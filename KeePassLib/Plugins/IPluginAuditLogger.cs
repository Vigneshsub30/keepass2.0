using System;
using System.Collections.Generic;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Structured audit logger for plugin lifecycle events.
	/// Implementations must be thread-safe.
	/// </summary>
	public interface IPluginAuditLogger
	{
		/// <summary>A load attempt was made for the given plugin path.</summary>
		void LogLoadAttempted(string pluginPath);

		/// <summary>A plugin passed all pre-execution gates and was admitted.</summary>
		/// <param name="pluginTypeName">
		/// The concrete <c>Plugin</c>-derived type found in the assembly.
		/// </param>
		/// <param name="publisherKeyToken">Hex key token of the signing publisher, or null.</param>
		void LogAdmitted(string pluginPath, string? pluginTypeName, string? publisherKeyToken);

		/// <summary>A plugin was rejected before any of its code executed.</summary>
		void LogRejected(string pluginPath, string reason);

		/// <summary>A plugin was cleanly unloaded.</summary>
		void LogUnloaded(string pluginPath);

		/// <summary>
		/// An unexpected error occurred during plugin processing.
		/// </summary>
		void LogError(string pluginPath, Exception exception);
	}
}
