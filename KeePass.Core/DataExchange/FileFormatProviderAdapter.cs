using KeePass.Core.Services;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// Wraps a concrete format name, display name, extension, and capabilities
	/// into the <see cref="IFileFormatProvider"/> contract. Used by the Avalonia
	/// DI container to expose available import/export formats without depending
	/// on the WinForms <c>FileFormatProvider</c> base class.
	/// </summary>
	public sealed class FileFormatProviderAdapter : IFileFormatProvider
	{
		public string FormatName { get; }
		public string DisplayName { get; }
		public bool SupportsImport { get; }
		public bool SupportsExport { get; }
		public string DefaultExtension { get; }

		public FileFormatProviderAdapter(
			string formatName,
			string displayName,
			bool supportsImport,
			bool supportsExport,
			string defaultExtension)
		{
			FormatName       = formatName ?? string.Empty;
			DisplayName      = displayName ?? formatName ?? string.Empty;
			SupportsImport   = supportsImport;
			SupportsExport   = supportsExport;
			DefaultExtension = defaultExtension ?? string.Empty;
		}
	}
}
