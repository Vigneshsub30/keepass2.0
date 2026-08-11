namespace KeePass.Plugins
{
	/// <summary>
	/// Type of signature found on a plugin assembly.
	/// </summary>
	public enum PluginSignatureType
	{
		/// <summary>No signature was found.</summary>
		None,

		/// <summary>
		/// Windows Authenticode (PE file signature, X.509 certificate).
		/// Available on Windows only; verified via
		/// <see cref="System.Security.Cryptography.X509Certificates.X509Certificate"/>.
		/// </summary>
		Authenticode,

		/// <summary>
		/// Cross-platform detached RSA-SHA256 signature.
		/// The signature file is placed next to the DLL with a <c>.sig</c>
		/// extension.
		/// </summary>
		DetachedRsa,
	}

	/// <summary>
	/// Result of plugin code-signing verification.
	/// </summary>
	public sealed class PluginSignatureResult
	{
		/// <summary>
		/// <see langword="true"/> when the signature is cryptographically
		/// valid and the publisher is in the allow-list (or the allow-list is
		/// empty and the signature is otherwise valid).
		/// </summary>
		public bool IsValid { get; }

		/// <summary>
		/// Hex-encoded public key token of the signing publisher, or
		/// <see langword="null"/> when no signature was found.
		/// </summary>
		public string? PublisherKeyToken { get; }

		/// <summary>
		/// Human-readable name of the publisher extracted from the certificate,
		/// if available.
		/// </summary>
		public string? PublisherName { get; }

		/// <summary>Type of signature that was verified.</summary>
		public PluginSignatureType SignatureType { get; }

		/// <summary>
		/// Reason for rejection when <see cref="IsValid"/> is
		/// <see langword="false"/>.  <see langword="null"/> when valid.
		/// </summary>
		public string? RejectionReason { get; }

		public PluginSignatureResult(
			bool isValid,
			string? publisherKeyToken,
			string? publisherName,
			PluginSignatureType signatureType,
			string? rejectionReason)
		{
			IsValid          = isValid;
			PublisherKeyToken = publisherKeyToken;
			PublisherName    = publisherName;
			SignatureType    = signatureType;
			RejectionReason  = rejectionReason;
		}

		public static PluginSignatureResult Valid(
			string? publisherKeyToken,
			string? publisherName,
			PluginSignatureType signatureType)
			=> new PluginSignatureResult(true, publisherKeyToken, publisherName, signatureType, null);

		public static PluginSignatureResult Invalid(
			PluginSignatureType signatureType,
			string reason)
			=> new PluginSignatureResult(false, null, null, signatureType, reason);
	}
}
