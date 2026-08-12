using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

using KeePass.Util;

using KeePassLib.Plugins;

namespace KeePass.Plugins
{
	/// <summary>
	/// Inspects a plugin assembly via <see cref="MetadataLoadContext"/> —
	/// without executing any code — and decides whether the assembly is
	/// admissible for loading.
	/// </summary>
	public static class PluginMetadataInspector
	{
		/// <summary>
		/// Assemblies that plugins are not allowed to reference directly.
		/// Plugins must interact through <see cref="IPluginHost"/> /
		/// <see cref="IPluginHostV2"/> abstractions.
		/// </summary>
		private static readonly HashSet<string> s_blockedAssemblies =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"System.Windows.Forms",
				"System.Drawing",
			};

		/// <summary>
		/// Inspects <paramref name="assemblyPath"/> without loading it for
		/// execution and returns the inspection verdict.
		/// </summary>
		/// <param name="assemblyPath">Absolute path to the plugin DLL.</param>
		public static PluginInspectionResult Inspect(string assemblyPath)
		{
			if (string.IsNullOrEmpty(assemblyPath))
				throw new ArgumentNullException(nameof(assemblyPath));

			var reasons = new List<string>();
			string? pluginTypeName       = null;
			string? targetFramework      = null;
			var     referencedAssemblies = new List<string>();

			// ── Step 1: Validate the file is a valid PE. ──────────────── //
			if (!File.Exists(assemblyPath))
			{
				reasons.Add($"File not found: {assemblyPath}");
				return PluginInspectionResult.Rejected(reasons);
			}

			// ── Step 2: MetadataLoadContext inspection. ───────────────── //
			try
			{
				// Collect all runtime assemblies so the resolver can satisfy
				// standard library references.
				string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
				string[] runtimeDlls = Directory.GetFiles(runtimeDir, "*.dll");

				// Also include the plugin's own directory.
				string pluginDir = Path.GetDirectoryName(assemblyPath)!;
				string[] pluginDirDlls = Directory.GetFiles(pluginDir, "*.dll");

				var allPaths = new string[runtimeDlls.Length + pluginDirDlls.Length + 1];
				runtimeDlls.CopyTo(allPaths, 0);
				pluginDirDlls.CopyTo(allPaths, runtimeDlls.Length);
				allPaths[allPaths.Length - 1] = WinUtil.GetExecutable();

				var resolver = new PathAssemblyResolver(allPaths);
				using var mlc = new MetadataLoadContext(resolver);

				Assembly asm = mlc.LoadFromAssemblyPath(assemblyPath);

				// ── TargetFramework ─────────────────────────────────── //
				foreach (CustomAttributeData attr in asm.CustomAttributes)
				{
					if (attr.AttributeType.FullName ==
						typeof(TargetFrameworkAttribute).FullName)
					{
						if (attr.ConstructorArguments.Count > 0)
							targetFramework = attr.ConstructorArguments[0].Value as string;
						break;
					}
				}

				// ── Referenced assemblies ───────────────────────────── //
				foreach (AssemblyName refName in asm.GetReferencedAssemblies())
				{
					string name = refName.Name ?? string.Empty;
					referencedAssemblies.Add(name);

					if (s_blockedAssemblies.Contains(name))
						reasons.Add($"Plugin references blocked assembly '{name}'. " +
							$"Use IPluginHostV2.ApplicationHost instead.");
				}

				// ── Plugin type detection ────────────────────────────── //
				const string pluginBaseFullName = "KeePass.Plugins.Plugin";
				try
				{
					foreach (TypeInfo t in asm.DefinedTypes)
					{
						if (t.IsAbstract) continue;

						Type? baseType = t.BaseType;
						while (baseType != null)
						{
							if (baseType.FullName == pluginBaseFullName)
							{
								pluginTypeName = t.FullName;
								break;
							}
							baseType = baseType.BaseType;
						}

						if (pluginTypeName != null) break;
					}
				}
				catch (ReflectionTypeLoadException) { /* partial results ok */ }

				if (pluginTypeName == null)
					reasons.Add("No concrete type deriving from KeePass.Plugins.Plugin found.");
			}
			catch (BadImageFormatException bif)
			{
				reasons.Add($"Assembly is not a valid .NET image: {bif.Message}");
				return PluginInspectionResult.Rejected(reasons);
			}
			catch (FileLoadException fle)
			{
				reasons.Add($"Assembly could not be loaded: {fle.Message}");
				return PluginInspectionResult.Rejected(reasons);
			}
			catch (Exception ex)
			{
				reasons.Add($"Unexpected inspection error: {ex.Message}");
				return PluginInspectionResult.Rejected(reasons);
			}

			bool admitted = reasons.Count == 0;
			if (admitted)
				return PluginInspectionResult.Admitted(pluginTypeName, referencedAssemblies, targetFramework);

			return new PluginInspectionResult(
				false, reasons, pluginTypeName, referencedAssemblies, targetFramework);
		}
	}
}
