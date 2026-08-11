#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using KeePass.Core.Logging;
using KeePassLib.Plugins;

using Xunit;

namespace KeePass.Tests.Platform
{
	public sealed class PluginAuditLoggerTests : IDisposable
	{
		private readonly string _tmpDir;
		private readonly string _logFile;
		private readonly string _appDir;

		public PluginAuditLoggerTests()
		{
			_appDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			_tmpDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			_logFile = Path.Combine(_tmpDir, "plugin-audit.log");
			Directory.CreateDirectory(_appDir);
		}

		public void Dispose()
		{
			try { Directory.Delete(_tmpDir, true); } catch { }
			try { Directory.Delete(_appDir, true); } catch { }
		}

		private PluginAuditLogger MakeLogger()
			=> new PluginAuditLogger(_logFile, _appDir);

		private static IReadOnlyList<JsonDocument> ReadLog(string path)
		{
			var docs = new List<JsonDocument>();
			if (!File.Exists(path)) return docs;
			foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				docs.Add(JsonDocument.Parse(line));
			}
			return docs;
		}

		// ── NullPluginAuditLogger ─────────────────────────────────────── //

		[Fact]
		public void Null_NoException_NoCalls()
		{
			IPluginAuditLogger logger = NullPluginAuditLogger.Instance;
			logger.LogLoadAttempted("plugin.dll");
			logger.LogAdmitted("plugin.dll", "MyPlugin", null);
			logger.LogRejected("plugin.dll", "Bad reference");
			logger.LogUnloaded("plugin.dll");
			logger.LogError("plugin.dll", new Exception("oops"));
		}

		[Fact]
		public void Null_IsAlwaysSameInstance()
		{
			Assert.Same(NullPluginAuditLogger.Instance, NullPluginAuditLogger.Instance);
		}

		// ── PluginAuditLogger — event types ──────────────────────────── //

		[Fact]
		public void Log_LoadAttempted_WritesEntry()
		{
			MakeLogger().LogLoadAttempted(Path.Combine(_appDir, "Plugin.dll"));
			var entries = ReadLog(_logFile);
			Assert.Single(entries);
			Assert.Equal("LoadAttempted", entries[0].RootElement.GetProperty("event").GetString());
		}

		[Fact]
		public void Log_Admitted_WritesPluginTypeAndToken()
		{
			MakeLogger().LogAdmitted(Path.Combine(_appDir, "Plugin.dll"),
				"MyPlugin.PluginExt", "aabb1122");
			var entries = ReadLog(_logFile);
			Assert.Single(entries);
			var root = entries[0].RootElement;
			Assert.Equal("Admitted",       root.GetProperty("event").GetString());
			Assert.Equal("MyPlugin.PluginExt", root.GetProperty("pluginType").GetString());
			Assert.Equal("aabb1122",       root.GetProperty("publisherToken").GetString());
		}

		[Fact]
		public void Log_Rejected_WritesReason()
		{
			MakeLogger().LogRejected(Path.Combine(_appDir, "Bad.dll"), "Blocked reference");
			var entries = ReadLog(_logFile);
			Assert.Single(entries);
			Assert.Equal("Rejected",        entries[0].RootElement.GetProperty("event").GetString());
			Assert.Equal("Blocked reference", entries[0].RootElement.GetProperty("reason").GetString());
		}

		[Fact]
		public void Log_Unloaded_WritesEntry()
		{
			MakeLogger().LogUnloaded(Path.Combine(_appDir, "Plugin.dll"));
			var entries = ReadLog(_logFile);
			Assert.Equal("Unloaded", entries[0].RootElement.GetProperty("event").GetString());
		}

		[Fact]
		public void Log_Error_WritesExceptionTypeAndMessage()
		{
			var ex = new InvalidOperationException("test failure");
			MakeLogger().LogError(Path.Combine(_appDir, "Plugin.dll"), ex);
			var entries = ReadLog(_logFile);
			Assert.Equal("Error", entries[0].RootElement.GetProperty("event").GetString());
			Assert.Contains("InvalidOperationException",
				entries[0].RootElement.GetProperty("exceptionType").GetString());
		}

		// ── Path redaction ────────────────────────────────────────────── //

		[Fact]
		public void Log_PathInsideAppDir_IsRelative()
		{
			string pluginPath = Path.Combine(_appDir, "plugins", "MyPlugin.dll");
			Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
			MakeLogger().LogLoadAttempted(pluginPath);
			var entries = ReadLog(_logFile);
			string? logged = entries[0].RootElement.GetProperty("path").GetString();
			Assert.NotNull(logged);
			// Should be a relative path, not contain the temp directory.
			Assert.DoesNotContain(_appDir, logged!);
		}

		[Fact]
		public void Log_PathOutsideAppDir_IsHashed()
		{
			string externalPath = Path.Combine(Path.GetTempPath(), "vault.kdbx");
			MakeLogger().LogLoadAttempted(externalPath);
			var entries = ReadLog(_logFile);
			string? logged = entries[0].RootElement.GetProperty("path").GetString();
			Assert.NotNull(logged);
			// Should be a SHA-256 hash, not the raw path.
			Assert.StartsWith("sha256:", logged!);
			Assert.DoesNotContain(Path.GetTempPath(), logged!);
		}

		// ── Log rotation ──────────────────────────────────────────────── //

		[Fact]
		public void Log_Rotation_CreatesRotatedFile()
		{
			// Write a very large log entry to trigger rotation.
			// Use a logger with a tiny threshold by writing directly, then
			// verify rotation logic works end-to-end with the 5MB limit.
			// We simulate having a pre-existing large log file.
			Directory.CreateDirectory(_tmpDir);
			// Create a fake 5MB+1 log file.
			byte[] bulk = new byte[5 * 1024 * 1024 + 1];
			File.WriteAllBytes(_logFile, bulk);

			// Writing one more entry should trigger rotation.
			MakeLogger().LogLoadAttempted(Path.Combine(_appDir, "Plugin.dll"));

			// The original large file should now be at .1
			Assert.True(File.Exists(_logFile + ".1"),
				"Rotation should have created .log.1");
			// A new small log file should exist.
			Assert.True(File.Exists(_logFile));
		}
	}
}
