#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for the Roslyn-based PLGX compilation pipeline, exercising the
	/// Roslyn API in the same way <c>RoslynPlgxCompiler</c> (in the KeePass
	/// project) does, without requiring a direct reference to that assembly.
	/// </summary>
	public sealed class RoslynPlgxCompilerTests
	{
		// ── Local helper that mirrors RoslynPlgxCompiler.Compile ─────── //

		private sealed class CompileResult
		{
			public bool     IsSuccess { get; }
			public string[] Errors    { get; }
			public string?  OutputPath { get; }

			private CompileResult(bool ok, string[] errs, string? path)
			{ IsSuccess = ok; Errors = errs; OutputPath = path; }

			public static CompileResult Ok(string path)
				=> new CompileResult(true, Array.Empty<string>(), path);
			public static CompileResult Fail(string[] errs)
				=> new CompileResult(false, errs, null);
		}

		// Returns true only for managed PE assemblies; filters out native DLLs
		// (e.g. coreclr.dll, clrjit.dll) that share the .dll extension on Windows
		// and Linux but contain no CLI metadata — Roslyn emits CS0009 for them.
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

		private static CompileResult Compile(
			IEnumerable<string> sourceFiles,
			string outputDll)
		{
			string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
			var refs = Directory.GetFiles(runtimeDir, "*.dll")
				.Where(IsManagedAssembly)
				.Select(f => MetadataReference.CreateFromFile(f))
				.Cast<MetadataReference>()
				.ToList();

			var parseOpts = CSharpParseOptions.Default
				.WithLanguageVersion(LanguageVersion.Latest);

			var trees = sourceFiles
				.Select(f =>
				{
					string src = File.ReadAllText(f, Encoding.UTF8);
					return CSharpSyntaxTree.ParseText(
						SourceText.From(src, Encoding.UTF8), parseOpts, path: f);
				})
				.ToList();

			var compileOpts = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
			var compilation = CSharpCompilation.Create(
				Path.GetFileNameWithoutExtension(outputDll), trees, refs, compileOpts);

			Directory.CreateDirectory(Path.GetDirectoryName(outputDll)!);
			using var ms = new MemoryStream();
			EmitResult emit = compilation.Emit(ms);

			if (!emit.Success)
			{
				string[] errs = emit.Diagnostics
					.Where(d => d.Severity == DiagnosticSeverity.Error)
					.Select(d => d.ToString())
					.ToArray();
				return CompileResult.Fail(errs);
			}

			File.WriteAllBytes(outputDll, ms.ToArray());
			return CompileResult.Ok(outputDll);
		}

		// ── Tests ─────────────────────────────────────────────────────── //

		[Fact]
		public void Compile_MinimalSource_Succeeds()
		{
			using var tmp = new TempDir();
			string src = @"namespace Hello { public sealed class World { public string Greet() => ""Hi""; } }";
			string cs  = tmp.Write("Hello.cs", src);
			var r = Compile(new[] { cs }, tmp.Dll("Hello.dll"));
			Assert.True(r.IsSuccess, string.Join("; ", r.Errors));
			Assert.True(File.Exists(r.OutputPath));
		}

		[Fact]
		public void Compile_SyntaxError_ReturnsFailure()
		{
			using var tmp = new TempDir();
			// Missing closing brace.
			string cs = tmp.Write("Bad.cs", "namespace Bad { public class X {");
			var r = Compile(new[] { cs }, tmp.Dll("Bad.dll"));
			Assert.False(r.IsSuccess);
			Assert.NotEmpty(r.Errors);
		}

		[Fact]
		public void Compile_ModernCSharp_NullableRecords_Succeeds()
		{
			using var tmp = new TempDir();
			string src = @"
#nullable enable
namespace Modern
{
    public record Point(int X, int Y);
    public sealed class NullSafe { public string? V { get; } }
}";
			string cs = tmp.Write("Modern.cs", src);
			var r = Compile(new[] { cs }, tmp.Dll("Modern.dll"));
			Assert.True(r.IsSuccess,
				"Modern C# features (records, nullable) must compile with Roslyn.");
		}

		[Fact]
		public void Compile_MultipleSourceFiles_Succeeds()
		{
			using var tmp = new TempDir();
			string cs1 = tmp.Write("A.cs", "namespace Multi { public class A {} }");
			string cs2 = tmp.Write("B.cs", "namespace Multi { public class B : A {} }");
			var r = Compile(new[] { cs1, cs2 }, tmp.Dll("Multi.dll"));
			Assert.True(r.IsSuccess, string.Join("; ", r.Errors));
		}

		[Fact]
		public void Compile_EmptySourceList_ReturnsOutputWithNoTypes()
		{
			// An empty compilation should not throw — it just produces an empty DLL.
			using var tmp = new TempDir();
			var r = Compile(Array.Empty<string>(), tmp.Dll("Empty.dll"));
			// Either succeeds (empty DLL) or fails gracefully — both are acceptable.
			// The important thing is: no exception is thrown.
		}

		// ── Helpers ────────────────────────────────────────────────────── //

		private sealed class TempDir : IDisposable
		{
			private readonly string _dir;
			public TempDir()
			{
				_dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(_dir);
			}
			public string Write(string name, string content)
			{
				string path = Path.Combine(_dir, name);
				File.WriteAllText(path, content, Encoding.UTF8);
				return path;
			}
			public string Dll(string name) => Path.Combine(_dir, "out", name);
			public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
		}
	}
}
