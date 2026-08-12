using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace KeePass.Plugins
{
	/// <summary>
	/// Compiles PLGX plugin source files using the Roslyn
	/// <see cref="CSharpCompilation"/> API, replacing the legacy
	/// <c>CSharpCodeProvider</c> (System.CodeDom.Compiler) pipeline.
	/// </summary>
	/// <remarks>
	/// PLGX is a deprecated format; a deprecation warning is emitted for
	/// every compilation.  The forward path is pre-compiled, signed DLL plugins.
	/// </remarks>
	public static class RoslynPlgxCompiler
	{
		/// <summary>
		/// Compiles a set of C# source files into a DLL and writes it to
		/// <paramref name="outputAssemblyPath"/>.
		/// </summary>
		/// <param name="sourceFilePaths">Paths to all <c>.cs</c> source files.</param>
		/// <param name="referencedAssemblyPaths">
		/// Paths to assemblies that should be referenced (in addition to the
		/// standard .NET runtime library set resolved automatically).
		/// </param>
		/// <param name="preprocessorSymbols">
		/// Conditional-compilation symbols (e.g. <c>["DEBUG", "TRACE"]</c>).
		/// </param>
		/// <param name="outputAssemblyPath">Output DLL path.</param>
		/// <returns>
		/// A <see cref="PlgxCompilationResult"/> describing success / failure.
		/// </returns>
		public static PlgxCompilationResult Compile(
			IEnumerable<string>  sourceFilePaths,
			IEnumerable<string>  referencedAssemblyPaths,
			IEnumerable<string>  preprocessorSymbols,
			string               outputAssemblyPath)
		{
			if (sourceFilePaths == null)        throw new ArgumentNullException(nameof(sourceFilePaths));
			if (referencedAssemblyPaths == null) throw new ArgumentNullException(nameof(referencedAssemblyPaths));
			if (preprocessorSymbols == null)    throw new ArgumentNullException(nameof(preprocessorSymbols));
			if (string.IsNullOrEmpty(outputAssemblyPath))
				throw new ArgumentNullException(nameof(outputAssemblyPath));

			// ── Parse source files ─────────────────────────────────── //
			var parseOptions = CSharpParseOptions.Default
				.WithLanguageVersion(LanguageVersion.Latest)
				.WithPreprocessorSymbols(preprocessorSymbols);

			var trees = new List<SyntaxTree>();
			foreach (string srcPath in sourceFilePaths)
			{
				string src = File.ReadAllText(srcPath, Encoding.UTF8);
				SyntaxTree tree = CSharpSyntaxTree.ParseText(
					SourceText.From(src, Encoding.UTF8),
					parseOptions,
					path: srcPath);
				trees.Add(tree);
			}

			if (trees.Count == 0)
				return PlgxCompilationResult.Failure(
					new[] { PlgxDiagnostic.Error(null, 0, 0, "No source files to compile.") });

		// ── Resolve references ─────────────────────────────────── //
		var refs = new List<MetadataReference>();

		// Add all managed .NET runtime framework assemblies.
		// IsManagedAssembly pre-filters native DLLs (e.g. coreclr.dll, clrjit.dll)
		// that share the .dll extension on Windows and Linux but contain no managed
		// metadata — passing them to Roslyn would produce CS0009 errors during emit.
		string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
		foreach (string dll in Directory.GetFiles(runtimeDir, "*.dll").Where(IsManagedAssembly))
			refs.Add(MetadataReference.CreateFromFile(dll));

			// Add caller-supplied references (host exe, cached dependency DLLs, etc.).
			foreach (string refPath in referencedAssemblyPaths)
			{
				if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath))
				{
					try { refs.Add(MetadataReference.CreateFromFile(refPath)); }
					catch { /* skip */ }
				}
			}

			// ── Compile ────────────────────────────────────────────── //
			string asmName = Path.GetFileNameWithoutExtension(outputAssemblyPath);
			CSharpCompilationOptions options = new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				optimizationLevel:  OptimizationLevel.Release,
				assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

			CSharpCompilation compilation = CSharpCompilation.Create(
				asmName,
				trees,
				refs,
				options);

			Directory.CreateDirectory(Path.GetDirectoryName(outputAssemblyPath)!);
			using var ms = new MemoryStream();
			EmitResult emitResult = compilation.Emit(ms);

			if (!emitResult.Success)
			{
				var diagnostics = emitResult.Diagnostics
					.Where(d => d.Severity == DiagnosticSeverity.Error)
					.Select(d =>
					{
						FileLinePositionSpan pos = d.Location.GetLineSpan();
						return PlgxDiagnostic.Error(
							pos.Path,
							pos.StartLinePosition.Line + 1,
							pos.StartLinePosition.Character + 1,
							d.GetMessage());
					})
					.ToArray();
				return PlgxCompilationResult.Failure(diagnostics);
			}

		File.WriteAllBytes(outputAssemblyPath, ms.ToArray());
		return PlgxCompilationResult.Success(outputAssemblyPath);
	}

	/// <summary>
	/// Returns <c>true</c> when <paramref name="path"/> points to a managed PE
	/// assembly (i.e. contains a CLI metadata header).  Returns <c>false</c> for
	/// native DLLs such as coreclr.dll or clrjit.dll, which share the .dll
	/// extension on Windows and Linux but would cause CS0009 errors if passed to
	/// Roslyn as metadata references.
	/// </summary>
	private static bool IsManagedAssembly(string path)
	{
		try
		{
			using var stream = File.OpenRead(path);
			using var reader = new PEReader(stream);
			return reader.HasMetadata;
		}
		catch { return false; }
	}
}

/// <summary>
/// A single compiler diagnostic (error or warning) from the Roslyn PLGX pipeline.
	/// </summary>
	public sealed class PlgxDiagnostic
	{
		public string? FilePath   { get; }
		public int     Line       { get; }
		public int     Column     { get; }
		public string  Message    { get; }

		private PlgxDiagnostic(string? file, int line, int col, string msg)
		{
			FilePath = file;
			Line     = line;
			Column   = col;
			Message  = msg;
		}

		public static PlgxDiagnostic Error(string? file, int line, int col, string msg)
			=> new PlgxDiagnostic(file, line, col, msg);

		public override string ToString()
		{
			if (string.IsNullOrEmpty(FilePath))
				return $"Error: {Message}";
			return $"{FilePath}({Line},{Column}): error: {Message}";
		}
	}

	/// <summary>
	/// Result of a <see cref="RoslynPlgxCompiler.Compile"/> invocation.
	/// </summary>
	public sealed class PlgxCompilationResult
	{
		public bool              IsSuccess         { get; }
		public string?           OutputAssemblyPath { get; }
		public PlgxDiagnostic[]  Errors            { get; }

		private PlgxCompilationResult(
			bool isSuccess,
			string? outputPath,
			PlgxDiagnostic[] errors)
		{
			IsSuccess          = isSuccess;
			OutputAssemblyPath = outputPath;
			Errors             = errors;
		}

		public static PlgxCompilationResult Success(string outputPath)
			=> new PlgxCompilationResult(true, outputPath, Array.Empty<PlgxDiagnostic>());

		public static PlgxCompilationResult Failure(IEnumerable<PlgxDiagnostic> errors)
			=> new PlgxCompilationResult(false, null, errors.ToArray());
	}
}
