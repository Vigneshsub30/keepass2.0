using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KeePass.Plugins
{
	/// <summary>
	/// Verifies the code-signing signature of a plugin assembly against a
	/// <see cref="PublisherKeyAllowList"/>.
	/// </summary>
	/// <remarks>
	/// Two signature mechanisms are supported:
	/// <list type="bullet">
	///   <item>
	///     <b>Authenticode</b> (Windows only): PE-embedded signature verified
	///     via <see cref="X509Certificate.CreateFromSignedFile"/>.
	///   </item>
	///   <item>
	///     <b>Detached RSA-SHA256</b> (cross-platform): a separate
	///     <c>&lt;assembly&gt;.sig</c> file whose bytes are verified against
	///     the raw SHA-256 hash of the assembly using RSA with PKCS#1 v1.5.
	///     The public key token is the lowercase hex of the last 8 bytes of
	///     the SHA-1 of the SubjectPublicKeyInfo.
	///   </item>
	/// </list>
	/// When the allow-list is empty, any valid signature (or absent signature)
	/// passes; the allow-list is considered advisory.
	/// </remarks>
	public static class PluginSignatureVerifier
	{
		/// <summary>
		/// Verifies the signature of <paramref name="assemblyPath"/> against
		/// <paramref name="allowList"/>.
		/// </summary>
		public static PluginSignatureResult Verify(
			string assemblyPath,
			PublisherKeyAllowList allowList)
		{
			if (string.IsNullOrEmpty(assemblyPath))
				throw new ArgumentNullException(nameof(assemblyPath));
			if (allowList == null)
				throw new ArgumentNullException(nameof(allowList));

			// ── Detached RSA-SHA256 (cross-platform) ──────────────── //
			string sigPath = assemblyPath + ".sig";
			if (File.Exists(sigPath))
			{
				PluginSignatureResult rsaResult = VerifyDetachedRsa(assemblyPath, sigPath);
				if (!rsaResult.IsValid) return rsaResult;

				if (!allowList.IsAllowed(rsaResult.PublisherKeyToken))
					return PluginSignatureResult.Invalid(
						PluginSignatureType.DetachedRsa,
						$"Publisher '{rsaResult.PublisherKeyToken}' is not in the allow-list.");

				return rsaResult;
			}

			// ── Authenticode (Windows only) ───────────────────────── //
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				PluginSignatureResult authResult = VerifyAuthenticode(assemblyPath);
				if (!authResult.IsValid) return authResult;

				if (!allowList.IsAllowed(authResult.PublisherKeyToken))
					return PluginSignatureResult.Invalid(
						PluginSignatureType.Authenticode,
						$"Publisher '{authResult.PublisherKeyToken}' is not in the allow-list.");

				return authResult;
			}

			// ── No signature — decide based on allow-list enforcement ─ //
			if (allowList.IsEmpty)
			{
				// Allow-list not enforced; unsigned plugins are accepted at
				// this layer (MetadataLoadContext inspection already ran).
				return PluginSignatureResult.Valid(null, null, PluginSignatureType.None);
			}

			return PluginSignatureResult.Invalid(
				PluginSignatureType.None,
				"No valid signature found. The publisher allow-list is enforced " +
				"— only signed plugins may load.");
		}

		// ── Private helpers ─────────────────────────────────────────── //

		private static PluginSignatureResult VerifyAuthenticode(string assemblyPath)
		{
			try
			{
				X509Certificate cert =
					X509Certificate.CreateFromSignedFile(assemblyPath);

				string? keyToken = GetPublicKeyToken(cert.GetPublicKey());
				string? subject  = cert.Subject;

				return PluginSignatureResult.Valid(keyToken, subject,
					PluginSignatureType.Authenticode);
			}
			catch (CryptographicException cx)
			{
				return PluginSignatureResult.Invalid(
					PluginSignatureType.Authenticode,
					$"Authenticode verification failed: {cx.Message}");
			}
			catch (Exception ex)
			{
				return PluginSignatureResult.Invalid(
					PluginSignatureType.Authenticode,
					$"Authenticode check error: {ex.Message}");
			}
		}

		private static PluginSignatureResult VerifyDetachedRsa(
			string assemblyPath, string sigPath)
		{
			try
			{
				byte[] assemblyBytes  = File.ReadAllBytes(assemblyPath);
				byte[] signatureBytes = File.ReadAllBytes(sigPath);

				// The .sig file format: 4-byte big-endian key length + DER-encoded
				// RSA public key (SubjectPublicKeyInfo) + RSA-SHA256 signature.
				if (signatureBytes.Length < 8)
					return PluginSignatureResult.Invalid(
						PluginSignatureType.DetachedRsa, "Signature file too short.");

				int keyLen = (signatureBytes[0] << 24) | (signatureBytes[1] << 16)
					| (signatureBytes[2] << 8) | signatureBytes[3];

				if (keyLen <= 0 || keyLen > signatureBytes.Length - 4)
					return PluginSignatureResult.Invalid(
						PluginSignatureType.DetachedRsa,
						"Signature file has invalid key length field.");

				byte[] pubKeyBytes = new byte[keyLen];
				Buffer.BlockCopy(signatureBytes, 4, pubKeyBytes, 0, keyLen);

				int sigOffset = 4 + keyLen;
				byte[] sigBytes = new byte[signatureBytes.Length - sigOffset];
				Buffer.BlockCopy(signatureBytes, sigOffset, sigBytes, 0, sigBytes.Length);

				using RSA rsa = RSA.Create();
				rsa.ImportSubjectPublicKeyInfo(pubKeyBytes, out _);

				bool valid = rsa.VerifyData(
					assemblyBytes,
					sigBytes,
					HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);

				if (!valid)
					return PluginSignatureResult.Invalid(
						PluginSignatureType.DetachedRsa,
						"Detached RSA signature is not valid.");

				string? keyToken = GetPublicKeyToken(pubKeyBytes);
				return PluginSignatureResult.Valid(keyToken, null,
					PluginSignatureType.DetachedRsa);
			}
			catch (CryptographicException cx)
			{
				return PluginSignatureResult.Invalid(
					PluginSignatureType.DetachedRsa,
					$"RSA verification error: {cx.Message}");
			}
			catch (Exception ex)
			{
				return PluginSignatureResult.Invalid(
					PluginSignatureType.DetachedRsa,
					$"Signature check error: {ex.Message}");
			}
		}

		/// <summary>
		/// Computes the public key token: last 8 bytes of the SHA-1 of
		/// <paramref name="rawPublicKey"/>, returned as lowercase hex.
		/// </summary>
		private static string? GetPublicKeyToken(byte[]? rawPublicKey)
		{
			if (rawPublicKey == null || rawPublicKey.Length == 0)
				return null;

			byte[] sha1 = SHA1.HashData(rawPublicKey);
			// Public key token = last 8 bytes, reversed (little-endian).
			byte[] token = new byte[8];
			for (int i = 0; i < 8; i++)
				token[i] = sha1[sha1.Length - 1 - i];

			return Convert.ToHexString(token).ToLowerInvariant();
		}
	}
}
