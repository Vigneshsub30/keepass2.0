namespace KeePass.Core.Services
{
	/// <summary>
	/// Platform-neutral description of a password-manager file format that can
	/// be imported from or exported to. Implemented by adapters that wrap
	/// <c>KeePass.DataExchange.FileFormatProvider</c> in the desktop project.
	/// Named <c>IFileFormatProvider</c> to avoid collision with
	/// <see cref="System.IFormatProvider"/>.
	/// </summary>
	public interface IFileFormatProvider
	{
		/// <summary>Unique technical name of the format.</summary>
		string FormatName { get; }

		/// <summary>Human-readable display name shown in the UI.</summary>
		string DisplayName { get; }

		/// <summary>Whether this format can be imported into a database.</summary>
		bool SupportsImport { get; }

		/// <summary>Whether this format can be exported from a database.</summary>
		bool SupportsExport { get; }

		/// <summary>
		/// Default file extension without the leading dot, e.g. <c>"csv"</c>.
		/// May be empty if the format has no standard extension.
		/// </summary>
		string DefaultExtension { get; }
	}
}
