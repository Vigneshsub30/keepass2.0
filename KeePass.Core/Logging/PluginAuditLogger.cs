using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using KeePassLib.Plugins;

namespace KeePass.Core.Logging
{
	/// <summary>
	/// File-backed implementation of <see cref="IPluginAuditLogger"/> that
	/// writes structured JSON Lines entries and rotates the log at 5 MB.
	/// </summary>
	/// <remarks>
	/// All writes are protected by an instance-level lock, making this class
	/// safe for concurrent use from multiple threads.
	/// Log entries never contain raw file paths that could expose the user's
	/// vault or home directory.  Paths are either made relative to the
	/// application directory, or (for database paths) replaced with their
	/// SHA-256 hex hash.
	/// </remarks>
	public sealed class PluginAuditLogger : IPluginAuditLogger
	{
		/// <summary>Maximum log file size before rotation (5 MiB).</summary>
		private const long MaxLogFileSizeBytes = 5 * 1024 * 1024;

		/// <summary>Number of rotated files to retain (e.g. .log.1 and .log.2).</summary>
		private const int MaxRotatedFiles = 2;

		private readonly string _logFilePath;
		private readonly string? _appDirectory;
		private readonly object _lock = new object();

		/// <param name="logFilePath">
		/// Absolute path to the log file.  Rotated files are named
		/// <c>&lt;logFilePath&gt;.1</c>, <c>&lt;logFilePath&gt;.2</c>, etc.
		/// </param>
		/// <param name="appDirectory">
		/// Optional base directory used to relativise plugin paths.  When
		/// <see langword="null"/>, the calling assembly's directory is used.
		/// </param>
		public PluginAuditLogger(string logFilePath, string? appDirectory = null)
		{
			_logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
			_appDirectory = appDirectory;
		}

		// ── IPluginAuditLogger ───────────────────────────────────────── //

		public void LogLoadAttempted(string pluginPath)
			=> Write(PluginAuditEventType.LoadAttempted, pluginPath,
				new Dictionary<string, string>());

		public void LogAdmitted(string pluginPath, string? pluginTypeName, string? publisherKeyToken)
			=> Write(PluginAuditEventType.Admitted, pluginPath,
				new Dictionary<string, string>
				{
					["pluginType"]    = pluginTypeName    ?? string.Empty,
					["publisherToken"] = publisherKeyToken ?? string.Empty,
				});

		public void LogRejected(string pluginPath, string reason)
			=> Write(PluginAuditEventType.Rejected, pluginPath,
				new Dictionary<string, string> { ["reason"] = reason });

		public void LogUnloaded(string pluginPath)
			=> Write(PluginAuditEventType.Unloaded, pluginPath,
				new Dictionary<string, string>());

		public void LogError(string pluginPath, Exception exception)
			=> Write(PluginAuditEventType.Error, pluginPath,
				new Dictionary<string, string>
				{
					["exceptionType"]    = exception?.GetType().Name ?? "Unknown",
					["exceptionMessage"] = exception?.Message        ?? string.Empty,
				});

		// ── Internals ────────────────────────────────────────────────── //

		private void Write(
			PluginAuditEventType  eventType,
			string                pluginPath,
			Dictionary<string, string> details)
		{
			string redactedPath = RedactPath(pluginPath);

			var entry = new PluginAuditEntry(
				DateTime.UtcNow, eventType, redactedPath,
				new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(details));

			string json = SerializeEntry(entry);

			lock (_lock)
			{
				RotateIfNeeded();
				string dir = Path.GetDirectoryName(_logFilePath)!;
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				File.AppendAllText(_logFilePath, json + "\n", Encoding.UTF8);
			}
		}

		private static string SerializeEntry(PluginAuditEntry e)
		{
			// Produce a compact JSON object on one line (JSON Lines format).
			var doc = new System.Collections.Generic.Dictionary<string, object?>
			{
				["ts"]    = e.Timestamp.ToString("O"),
				["event"] = e.EventType.ToString(),
				["path"]  = e.PluginPath,
			};
			foreach (var kv in e.Details)
				doc[kv.Key] = kv.Value;

			return JsonSerializer.Serialize(doc);
		}

		private void RotateIfNeeded()
		{
			// Called inside the lock.
			if (!File.Exists(_logFilePath)) return;

			FileInfo fi = new FileInfo(_logFilePath);
			if (fi.Length < MaxLogFileSizeBytes) return;

			// Delete oldest retained file.
			string oldest = _logFilePath + "." + MaxRotatedFiles;
			if (File.Exists(oldest)) File.Delete(oldest);

			// Shift existing rotated files up.
			for (int i = MaxRotatedFiles - 1; i >= 1; i--)
			{
				string src  = _logFilePath + "." + i;
				string dest = _logFilePath + "." + (i + 1);
				if (File.Exists(src)) File.Move(src, dest);
			}

			// Rotate current log to .1.
			File.Move(_logFilePath, _logFilePath + ".1");
		}

		/// <summary>
		/// Converts <paramref name="rawPath"/> to a relative path (relative to
		/// <see cref="_appDirectory"/>) if possible, or hashes it otherwise.
		/// </summary>
		private string RedactPath(string rawPath)
		{
			if (string.IsNullOrEmpty(rawPath)) return string.Empty;

			string baseDir = _appDirectory
				?? Path.GetDirectoryName(
					typeof(PluginAuditLogger).Assembly.Location) ?? string.Empty;

			try
			{
				if (!string.IsNullOrEmpty(baseDir))
				{
					string full = Path.GetFullPath(rawPath);
					string baseF = Path.GetFullPath(baseDir);
					if (full.StartsWith(baseF, StringComparison.OrdinalIgnoreCase))
					{
						string rel = full.Substring(baseF.Length).TrimStart(
							Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
						return rel;
					}
				}
			}
			catch { /* fall through to hash */ }

			// Path is outside the app dir — hash it to protect the user's
			// home directory structure from appearing in logs.
			byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawPath));
			return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
		}
	}
}
