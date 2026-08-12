namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Platform-neutral record of which key sources are associated with a
	/// specific database path. Mirrors the information stored in
	/// <c>AceKeyAssoc</c> in the WinForms application layer without
	/// importing that assembly.
	/// </summary>
	public sealed class KeyAssociationData
	{
		/// <summary>Database path this association applies to.</summary>
		public string DatabasePath { get; init; } = string.Empty;

		/// <summary>Whether a master password is part of the composite key.</summary>
		public bool HasPassword { get; init; }

		/// <summary>
		/// Full path to the key file, or empty string when no key file is used.
		/// </summary>
		public string KeyFilePath { get; init; } = string.Empty;

		/// <summary>Whether the Windows user account key component is used.</summary>
		public bool UseUserAccount { get; init; }

		public KeyAssociationData() { }
	}
}
