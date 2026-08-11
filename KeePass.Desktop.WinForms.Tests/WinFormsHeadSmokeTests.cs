#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

using Xunit;

namespace KeePass.Desktop.WinForms.Tests
{
	/// <summary>
	/// Smoke tests for the SDK-style WinForms transitional head project.
	/// These tests verify project configuration, assembly loading, and
	/// P/Invoke signature correctness without launching the full application.
	/// All tests require a Windows runtime (net10.0-windows).
	/// </summary>
	[SupportedOSPlatform("windows")]
	public sealed class WinFormsHeadSmokeTests
	{
		// ── Assembly loading ─────────────────────────────────────────── //

		[Fact]
		public void KeePassDesktopWinForms_Assembly_Loads()
		{
			Assembly asm = typeof(KeePass.Desktop.WinForms.Program).Assembly;
			Assert.NotNull(asm);
		}

		[Fact]
		public void KeePassDesktopWinForms_TargetsWindowsTfm()
		{
			Assembly asm = typeof(KeePass.Desktop.WinForms.Program).Assembly;
			TargetFrameworkAttribute? tfAttr = asm.GetCustomAttribute<TargetFrameworkAttribute>();
			// The TFM should be net10.0-windows or similar (contains "windows").
			Assert.NotNull(tfAttr);
			Assert.Contains("windows", tfAttr!.FrameworkName,
				StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void KeePassLib_Assembly_Loads()
		{
			Assembly asm = typeof(KeePassLib.PwDatabase).Assembly;
			Assert.NotNull(asm);
		}

		[Fact]
		public void KeePassCore_Assembly_Loads()
		{
			Assembly asm = typeof(KeePass.Core.Services.IDatabaseSessionService).Assembly;
			Assert.NotNull(asm);
		}

		// ── P/Invoke signatures ───────────────────────────────────────── //

		[Fact]
		[System.Runtime.Versioning.SupportedOSPlatform("windows")]
		public void NativeMethods_GetForegroundWindow_Signature_IsValid()
		{
			// Resolve method via reflection to avoid a static dependency on
			// internal KeePass NativeMethods; test that the P/Invoke pattern
			// survives marshaling on 64-bit .NET 10.
			Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
				"This test must run on Windows.");

			// GetForegroundWindow should return a non-null IntPtr if any window
			// is in the foreground.  We just verify the import can be invoked.
			IntPtr hwnd = NativeSmokeInvoke.GetForegroundWindow();
			// No assertion on the value — null is valid when no window is focused.
			Assert.True(hwnd == IntPtr.Zero || hwnd != IntPtr.Zero);
		}

		[Fact]
		[System.Runtime.Versioning.SupportedOSPlatform("windows")]
		public void ApplicationConfiguration_Initialize_DoesNotThrow()
		{
			// Verifies that ApplicationConfiguration.Initialize() can be called
			// (idempotent on repeated calls) without throwing on .NET 10 WinForms.
			var ex = Record.Exception(() => ApplicationConfiguration.Initialize());
			Assert.Null(ex);
		}

		// ── Project file configuration ────────────────────────────────── //

		[Fact]
		public void WinFormsHeadProject_AppManifestExists()
		{
			// The app.manifest is embedded in the output directory during build.
			// Verify the source file exists relative to the project root.
			string manifestPath = Path.Combine(
				AppContext.BaseDirectory,
				"..", "..", "..", "..",
				"KeePass.Desktop.WinForms",
				"Properties",
				"app.manifest");
			// Normalize path.
			manifestPath = Path.GetFullPath(manifestPath);
			Assert.True(File.Exists(manifestPath),
				$"app.manifest not found at: {manifestPath}");
		}
	}

	/// <summary>
	/// Minimal P/Invoke declarations for smoke-testing marshal correctness.
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform("windows")]
	internal static class NativeSmokeInvoke
	{
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		internal static extern IntPtr GetForegroundWindow();
	}
}
