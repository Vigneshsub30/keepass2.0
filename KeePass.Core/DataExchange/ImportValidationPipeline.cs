using System;
using System.IO;
using System.Xml;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// The import trust-boundary gate.  Wraps a raw input stream in a
	/// <see cref="SizeLimitingStream"/> that enforces
	/// <see cref="ImportValidationOptions.MaxFileSize"/> and exposes factory
	/// methods for XML readers that enforce DTD prohibition and
	/// <see cref="ImportValidationOptions.MaxXmlDepth"/>.
	/// </summary>
	/// <remarks>
	/// Usage pattern in a provider integration point:
	/// <code>
	///   var pipeline = new ImportValidationPipeline(options);
	///   using var limited = pipeline.Validate(rawStream);
	///   // pass `limited` to the provider instead of `rawStream`
	/// </code>
	/// For XML providers:
	/// <code>
	///   using var xmlReader = pipeline.CreateXmlReader(limited);
	///   // use xmlReader as normal
	/// </code>
	/// </remarks>
	public sealed class ImportValidationPipeline
	{
		private readonly ImportValidationOptions _options;

		/// <param name="options">Ceiling configuration; must not be null.</param>
		public ImportValidationPipeline(ImportValidationOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
		}

		/// <summary>
		/// Wraps <paramref name="rawStream"/> in a
		/// <see cref="SizeLimitingStream"/> capped at
		/// <see cref="ImportValidationOptions.MaxFileSize"/> bytes.
		/// </summary>
		/// <returns>
		/// A read-only stream that throws
		/// <see cref="ImportValidationException"/> if the ceiling is exceeded.
		/// The caller does <em>not</em> own <paramref name="rawStream"/>'s
		/// lifetime through the returned wrapper — dispose the wrapper only;
		/// the underlying stream is left open.
		/// </returns>
		public SizeLimitingStream Validate(Stream rawStream)
		{
			if(rawStream is null) throw new ArgumentNullException(nameof(rawStream));
			return new SizeLimitingStream(rawStream, _options.MaxFileSize);
		}

		/// <summary>
		/// Returns <see cref="XmlReaderSettings"/> that:
		/// <list type="bullet">
		///   <item>Set <see cref="XmlReaderSettings.DtdProcessing"/> to
		///     <see cref="DtdProcessing.Prohibit"/> when
		///     <see cref="ImportValidationOptions.ProhibitDtd"/> is true.</item>
		///   <item>Cap <see cref="XmlReaderSettings.MaxCharactersFromEntities"/>
		///     at 10 000 to bound entity-expansion amplification.</item>
		///   <item>Disable external DTD/schema resolution.</item>
		/// </list>
		/// </summary>
		public XmlReaderSettings CreateXmlReaderSettings()
		{
			var settings = new XmlReaderSettings
			{
				DtdProcessing = _options.ProhibitDtd
					? DtdProcessing.Prohibit
					: DtdProcessing.Ignore,
				MaxCharactersFromEntities = 10_000L,
				XmlResolver = null,
				IgnoreComments = false,
				IgnoreWhitespace = false,
			};
			return settings;
		}

		/// <summary>
		/// Creates an <see cref="XmlReader"/> from <paramref name="stream"/>
		/// using the hardened settings from
		/// <see cref="CreateXmlReaderSettings"/> and wraps it in a
		/// <see cref="DepthLimitingXmlReader"/> capped at
		/// <see cref="ImportValidationOptions.MaxXmlDepth"/>.
		/// </summary>
		public DepthLimitingXmlReader CreateXmlReader(Stream stream)
		{
			if(stream is null) throw new ArgumentNullException(nameof(stream));
			XmlReaderSettings settings = CreateXmlReaderSettings();
			XmlReader inner = XmlReader.Create(stream, settings);
			return new DepthLimitingXmlReader(inner, _options.MaxXmlDepth);
		}
	}
}
