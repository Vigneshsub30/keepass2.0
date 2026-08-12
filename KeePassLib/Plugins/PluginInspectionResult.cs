using System.Collections.Generic;

namespace KeePassLib.Plugins
{
	/// <summary>
	/// Immutable result of a pre-execution plugin assembly inspection.
	/// </summary>
	public sealed class PluginInspectionResult
	{
		/// <summary>
		/// <see langword="true"/> if the assembly passed all pre-execution
		/// checks and is safe to load for execution.
		/// </summary>
		public bool IsAdmitted { get; }

		/// <summary>
		/// Human-readable reasons for rejection.  Empty when
		/// <see cref="IsAdmitted"/> is <see langword="true"/>.
		/// </summary>
		public IReadOnlyList<string> RejectionReasons { get; }

		/// <summary>
		/// Assembly-qualified name of the first type deriving from the
		/// KeePass <see cref="Plugin"/> base class found in the assembly,
		/// or <see langword="null"/> if none was found.
		/// </summary>
		public string? PluginTypeName { get; }

		/// <summary>
		/// Names of all assemblies referenced by the inspected assembly.
		/// </summary>
		public IReadOnlyList<string> ReferencedAssemblies { get; }

		/// <summary>
		/// Value of <c>TargetFrameworkAttribute</c> found on the assembly,
		/// e.g. ".NETCoreApp,Version=v10.0".  <see langword="null"/> if the
		/// attribute is absent.
		/// </summary>
		public string? TargetFramework { get; }

		public PluginInspectionResult(
			bool isAdmitted,
			IReadOnlyList<string> rejectionReasons,
			string? pluginTypeName,
			IReadOnlyList<string> referencedAssemblies,
			string? targetFramework)
		{
			IsAdmitted           = isAdmitted;
			RejectionReasons     = rejectionReasons;
			PluginTypeName       = pluginTypeName;
			ReferencedAssemblies = referencedAssemblies;
			TargetFramework      = targetFramework;
		}

		/// <summary>Convenience factory for a passing result.</summary>
		public static PluginInspectionResult Admitted(
			string? pluginTypeName,
			IReadOnlyList<string> referencedAssemblies,
			string? targetFramework)
			=> new PluginInspectionResult(
				true,
				System.Array.Empty<string>(),
				pluginTypeName,
				referencedAssemblies,
				targetFramework);

		/// <summary>Convenience factory for a rejected result.</summary>
		public static PluginInspectionResult Rejected(IReadOnlyList<string> reasons)
			=> new PluginInspectionResult(
				false,
				reasons,
				null,
				System.Array.Empty<string>(),
				null);
	}
}
