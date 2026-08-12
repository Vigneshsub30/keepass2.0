using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Installs (or removes) Chrome/Firefox Native Messaging host manifests
	/// that point to the keepass-proxy binary.  The manifest tells the browser
	/// where to find the proxy and which extension IDs are allowed.
	/// </summary>
	internal static class NativeMessagingInstaller
	{
		private const string HostName = "org.keepassxc.keepassxc_browser";

		// KeePassXC-Browser extension IDs
		private static readonly string[] ChromeOrigins = new[]
		{
			"chrome-extension://oboonakemofpalcgghocfoadofidjkkk/",
			"chrome-extension://pdffhmdngciaglkoonimfcmckehcpafo/"
		};

		private static readonly string[] FirefoxExtensions = new[]
		{
			"keepassxc-browser@keepassxc.org"
		};

		/// <summary>
		/// Installs manifest files for all supported browsers.
		/// Returns a list of paths that were written.
		/// </summary>
		public static List<string> Install(string proxyPath)
		{
			if (!File.Exists(proxyPath))
				throw new FileNotFoundException("Proxy binary not found", proxyPath);

			var written = new List<string>();

			foreach (string dir in GetChromiumManifestDirectories())
			{
				string path = WriteManifest(dir, proxyPath, ChromeOrigins, null);
				if (path != null) written.Add(path);
			}

			foreach (string dir in GetFirefoxManifestDirectories())
			{
				string path = WriteManifest(dir, proxyPath, null, FirefoxExtensions);
				if (path != null) written.Add(path);
			}

			return written;
		}

		/// <summary>
		/// Removes all installed manifest files.
		/// Returns a list of paths that were deleted.
		/// </summary>
		public static List<string> Uninstall()
		{
			var removed = new List<string>();
			var allDirs = new List<string>();
			allDirs.AddRange(GetChromiumManifestDirectories());
			allDirs.AddRange(GetFirefoxManifestDirectories());

			foreach (string dir in allDirs)
			{
				string file = Path.Combine(dir, HostName + ".json");
				if (File.Exists(file))
				{
					try
					{
						File.Delete(file);
						removed.Add(file);
					}
					catch { /* best effort */ }
				}
			}

			return removed;
		}

		/// <summary>Checks which browsers have a manifest installed.</summary>
		public static List<string> GetInstalledBrowsers()
		{
			var installed = new List<string>();
			var allDirs = new List<string>();
			allDirs.AddRange(GetChromiumManifestDirectories());
			allDirs.AddRange(GetFirefoxManifestDirectories());

			foreach (string dir in allDirs)
			{
				string file = Path.Combine(dir, HostName + ".json");
				if (File.Exists(file))
					installed.Add(dir);
			}

			return installed;
		}

		/// <summary>
		/// Returns the default proxy binary path inside the .app bundle (macOS)
		/// or alongside the main executable (Linux/Windows).
		/// </summary>
		public static string GetDefaultProxyPath()
		{
			string exeDir = AppContext.BaseDirectory;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// Inside .app bundle: Contents/MacOS/keepass-proxy
				return Path.Combine(exeDir, "keepass-proxy");
			}

			string proxyName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? "keepass-proxy.exe" : "keepass-proxy";
			return Path.Combine(exeDir, proxyName);
		}

		private static string WriteManifest(
			string directory, string proxyPath,
			string[] allowedOrigins, string[] allowedExtensions)
		{
			try
			{
				Directory.CreateDirectory(directory);

				var manifest = new Dictionary<string, object>
				{
					["name"] = HostName,
					["description"] = "KeePass browser integration",
					["path"] = proxyPath,
					["type"] = "stdio"
				};

				if (allowedOrigins != null)
					manifest["allowed_origins"] = allowedOrigins;
				if (allowedExtensions != null)
					manifest["allowed_extensions"] = allowedExtensions;

				string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
				{
					WriteIndented = true
				});

				string file = Path.Combine(directory, HostName + ".json");
				File.WriteAllText(file, json);
				return file;
			}
			catch
			{
				return null;
			}
		}

		private static IEnumerable<string> GetChromiumManifestDirectories()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				string appSupport = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					"Library", "Application Support");

				yield return Path.Combine(appSupport, "Google", "Chrome", "NativeMessagingHosts");
				yield return Path.Combine(appSupport, "Chromium", "NativeMessagingHosts");
				yield return Path.Combine(appSupport, "Microsoft Edge", "NativeMessagingHosts");
				yield return Path.Combine(appSupport, "BraveSoftware", "Brave-Browser", "NativeMessagingHosts");
				yield return Path.Combine(appSupport, "Vivaldi", "NativeMessagingHosts");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
					?? Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
						".config");

				yield return Path.Combine(config, "google-chrome", "NativeMessagingHosts");
				yield return Path.Combine(config, "chromium", "NativeMessagingHosts");
				yield return Path.Combine(config, "microsoft-edge", "NativeMessagingHosts");
				yield return Path.Combine(config, "BraveSoftware", "Brave-Browser", "NativeMessagingHosts");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// Windows uses the registry; for simplicity, we write to a common path
				// and rely on the registry being set up separately or by the installer
				string localApp = Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData);
				yield return Path.Combine(localApp, "Google", "Chrome", "User Data", "NativeMessagingHosts");
				yield return Path.Combine(localApp, "Microsoft", "Edge", "User Data", "NativeMessagingHosts");
			}
		}

		private static IEnumerable<string> GetFirefoxManifestDirectories()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				yield return Path.Combine(home, "Library", "Application Support",
					"Mozilla", "NativeMessagingHosts");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				yield return Path.Combine(home, ".mozilla", "native-messaging-hosts");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string appData = Environment.GetFolderPath(
					Environment.SpecialFolder.ApplicationData);
				yield return Path.Combine(appData, "Mozilla", "NativeMessagingHosts");
			}
		}
	}
}
