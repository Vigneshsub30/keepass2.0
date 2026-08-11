using System;
using System.Windows.Forms;

namespace KeePass.Desktop.WinForms
{
	/// <summary>
	/// Entry point for the SDK-style WinForms transitional head.
	/// This thin shell configures the WinForms runtime and delegates to the
	/// existing <c>KeePass.Program</c> startup sequence, allowing incremental
	/// migration toward the Avalonia cross-platform head while preserving all
	/// existing Windows-specific behaviour (auto-type, global hot-keys,
	/// clipboard operations, DPI handling, session lock detection).
	/// </summary>
	public static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			// .NET 10 WinForms: explicit DPI configuration replaces the
			// manifest-based approach.  PerMonitorV2 matches the .NET
			// Framework default set by the KeePass app.manifest.
			ApplicationConfiguration.Initialize();

			// Delegate to the existing KeePass startup sequence.
			// KeePass.Program.Main handles command-line parsing, mutex
			// single-instance enforcement, config loading, and MainForm setup.
			KeePass.Program.Main(args);
		}
	}
}
