using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace KeePass.Plugins
{
	/// <summary>
	/// A collectible <see cref="AssemblyLoadContext"/> that isolates a single
	/// plugin and its private dependencies from other plugins and from the
	/// host application.
	/// </summary>
	/// <remarks>
	/// <b>Sharing contract:</b> assemblies whose names appear in
	/// <c>hostAssemblyNames</c> are resolved from the <em>default</em>
	/// <see cref="AssemblyLoadContext"/> so that types like
	/// <see cref="KeePass.Plugins.Plugin"/> and
	/// <see cref="KeePass.Plugins.IPluginHost"/> are shared across all
	/// plugin contexts and the host.  All other assemblies are resolved from
	/// the plugin's directory first, then fall back to the default context.
	/// </remarks>
	public sealed class PluginLoadContext : AssemblyLoadContext
	{
		private readonly string _pluginDirectory;

		// Names of host assemblies that must resolve from the default context.
		private readonly System.Collections.Generic.HashSet<string> _hostAssemblyNames;

		private static readonly AssemblyLoadContext s_default =
			AssemblyLoadContext.Default;

		public PluginLoadContext(string pluginFilePath)
			: base(name: Path.GetFileNameWithoutExtension(pluginFilePath),
				   isCollectible: true)
		{
			_pluginDirectory = Path.GetDirectoryName(
				Path.GetFullPath(pluginFilePath))!;

			// These assemblies must come from the host so that
			// Plugin.Initialize(IPluginHost) signature types match.
			_hostAssemblyNames = new System.Collections.Generic.HashSet<string>(
				StringComparer.OrdinalIgnoreCase)
			{
				"KeePass",
				"KeePassLib",
				"KeePass.Core",
				"KeePass.Platform.Unix",
			};
		}

		/// <inheritdoc/>
		protected override Assembly? Load(AssemblyName assemblyName)
		{
			string name = assemblyName.Name ?? string.Empty;

			// Host contract assemblies must come from the default context so
			// all plugins share the same Plugin base class identity.
			if (_hostAssemblyNames.Contains(name))
				return null; // null → runtime falls back to the default ALC

			// Try to satisfy from the plugin's local directory first.
			string localPath = Path.Combine(_pluginDirectory, name + ".dll");
			if (File.Exists(localPath))
				return LoadFromAssemblyPath(localPath);

			// Fall back to the default context for framework assemblies.
			return null;
		}
	}
}
