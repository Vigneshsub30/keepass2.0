#nullable enable

using System;
using System.Collections.Generic;

using KeePassLib.Plugins;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="PluginInspectionResult"/> data class.
	/// These tests do not require actual assembly files — they exercise the
	/// record's factories, properties, and invariants.
	/// </summary>
	public sealed class PluginInspectionResultTests
	{
		[Fact]
		public void Admitted_SetsIsAdmittedTrue_EmptyReasons()
		{
			var result = PluginInspectionResult.Admitted(
				"MyPlugin", new[] { "KeePassLib" }, ".NETCoreApp,Version=v10.0");

			Assert.True(result.IsAdmitted);
			Assert.Empty(result.RejectionReasons);
			Assert.Equal("MyPlugin", result.PluginTypeName);
			Assert.Contains("KeePassLib", result.ReferencedAssemblies);
			Assert.Equal(".NETCoreApp,Version=v10.0", result.TargetFramework);
		}

		[Fact]
		public void Rejected_SetsIsAdmittedFalse_HasReasons()
		{
			var reasons = new[] { "Missing Plugin type", "Bad reference" };
			var result = PluginInspectionResult.Rejected(reasons);

			Assert.False(result.IsAdmitted);
			Assert.Equal(2, result.RejectionReasons.Count);
			Assert.Contains("Missing Plugin type", result.RejectionReasons);
			Assert.Null(result.PluginTypeName);
		}

		[Fact]
		public void FullConstructor_AllPropertiesRoundTrip()
		{
			var reasons = new List<string> { "reason1" };
			var refs    = new List<string> { "mscorlib" };

			var result = new PluginInspectionResult(
				false, reasons, "MyType", refs, "net10.0");

			Assert.False(result.IsAdmitted);
			Assert.Single(result.RejectionReasons);
			Assert.Equal("MyType",   result.PluginTypeName);
			Assert.Single(result.ReferencedAssemblies);
			Assert.Equal("net10.0", result.TargetFramework);
		}

		[Fact]
		public void Admitted_PluginTypeName_CanBeNull()
		{
			// Edge: a valid plugin assembly with no visible Plugin subtype yet
			// (shouldn't normally happen, but the model allows it).
			var result = PluginInspectionResult.Admitted(null, Array.Empty<string>(), null);
			Assert.Null(result.PluginTypeName);
			Assert.Null(result.TargetFramework);
		}

		[Fact]
		public void Rejected_ReferencedAssemblies_IsEmpty()
		{
			var result = PluginInspectionResult.Rejected(new[] { "bad" });
			Assert.Empty(result.ReferencedAssemblies);
		}
	}
}
