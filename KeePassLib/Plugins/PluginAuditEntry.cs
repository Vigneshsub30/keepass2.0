using System;
using System.Collections.Generic;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Represents a single structured plugin audit log entry.
	/// </summary>
	public enum PluginAuditEventType
	{
		LoadAttempted,
		Admitted,
		Rejected,
		Unloaded,
		Error,
	}

	/// <summary>
	/// An immutable snapshot of one plugin lifecycle event for logging.
	/// </summary>
	public sealed class PluginAuditEntry
	{
		/// <summary>UTC timestamp of the event.</summary>
		public DateTime Timestamp { get; }

		/// <summary>Type of the lifecycle event.</summary>
		public PluginAuditEventType EventType { get; }

		/// <summary>
		/// Redacted plugin path (relative to the application directory, or
		/// hashed if the path cannot be made relative).
		/// </summary>
		public string PluginPath { get; }

		/// <summary>
		/// Additional structured key/value details.  Values must not contain
		/// sensitive data (paths are pre-redacted, credentials are never stored).
		/// </summary>
		public IReadOnlyDictionary<string, string> Details { get; }

		public PluginAuditEntry(
			DateTime                    timestamp,
			PluginAuditEventType        eventType,
			string                      pluginPath,
			IReadOnlyDictionary<string, string> details)
		{
			Timestamp  = timestamp;
			EventType  = eventType;
			PluginPath = pluginPath ?? string.Empty;
			Details    = details   ?? new Dictionary<string, string>();
		}
	}
}
