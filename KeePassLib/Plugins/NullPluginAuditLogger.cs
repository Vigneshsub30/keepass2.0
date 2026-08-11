using System;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// No-op implementation of <see cref="IPluginAuditLogger"/> used when
	/// plugin audit logging is disabled.
	/// </summary>
	public sealed class NullPluginAuditLogger : IPluginAuditLogger
	{
		/// <summary>Singleton instance; safe for concurrent use.</summary>
		public static readonly NullPluginAuditLogger Instance = new NullPluginAuditLogger();

		private NullPluginAuditLogger() { }

		public void LogLoadAttempted(string pluginPath)                                          { }
		public void LogAdmitted(string pluginPath, string? pluginTypeName, string? keyToken)     { }
		public void LogRejected(string pluginPath, string reason)                                { }
		public void LogUnloaded(string pluginPath)                                               { }
		public void LogError(string pluginPath, Exception exception)                             { }
	}
}
