using System;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// Configurable ceilings applied by <see cref="ImportValidationPipeline"/>
	/// before any file-format provider processes an import stream.
	/// </summary>
	/// <remarks>
	/// Default values are chosen to allow all realistic password databases while
	/// still providing a meaningful guard against hostile or malformed inputs:
	/// <list type="bullet">
	///   <item>500 MB — far above any real-world KDBX export; protects against
	///     OOM via memory-mapped XML parsing.</item>
	///   <item>1 000 000 entries — an extreme upper bound; a 1M-entry CSV can
	///     legitimately be imported but anything above is almost certainly an
	///     attack or a mistake.</item>
	///   <item>100 XML levels — deeply nested XML is a common entity-expansion
	///     amplifier; no password-manager format uses more than ~10 levels.</item>
	///   <item>DTD prohibition — DTD entity expansion (billion-laughs) is the
	///     canonical XML DoS vector; no supported import format requires DTDs.</item>
	/// </list>
	/// </remarks>
	public sealed class ImportValidationOptions
	{
		/// <summary>
		/// Maximum number of bytes that may be read from the raw input stream.
		/// Default: 500 MB (524 288 000 bytes).
		/// </summary>
		public long MaxFileSize { get; set; } = 524_288_000L; // 500 MB

		/// <summary>
		/// Maximum number of entries that a provider may commit to the database.
		/// Checked by the pipeline after Import() returns.
		/// Default: 1 000 000.
		/// </summary>
		public int MaxEntryCount { get; set; } = 1_000_000;

		/// <summary>
		/// Maximum XML element nesting depth allowed for XML-based providers.
		/// Default: 100.
		/// </summary>
		public int MaxXmlDepth { get; set; } = 100;

		/// <summary>
		/// When true, any XML input that contains a DTD declaration is rejected
		/// before the provider receives the data.
		/// Default: true.
		/// </summary>
		public bool ProhibitDtd { get; set; } = true;

		/// <summary>
		/// Returns a new <see cref="ImportValidationOptions"/> with all defaults.
		/// </summary>
		public static ImportValidationOptions Default() => new ImportValidationOptions();
	}
}
