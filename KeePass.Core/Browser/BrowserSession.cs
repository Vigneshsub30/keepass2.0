using System;
using System.Collections.Generic;

namespace KeePass.Core.Browser
{
	/// <summary>
	/// Per-client session state for a connected browser.
	/// Tracks the client public key, association credentials, and the
	/// NaCl crypto instance used for message encryption/decryption.
	/// </summary>
	public sealed class BrowserSession : IDisposable
	{
		private readonly NaClCrypto _crypto;

		public BrowserSession()
		{
			_crypto = new NaClCrypto();
		}

		/// <summary>
		/// Creates a session that shares the server key pair from an existing
		/// <see cref="NaClCrypto"/> instance.  This ensures a stable server
		/// public key across independent socket connections.
		/// </summary>
		public BrowserSession(NaClCrypto sharedKeys)
		{
			_crypto = new NaClCrypto(sharedKeys.ServerSecretKey, sharedKeys.ServerPublicKey);
		}

		/// <summary>Client-supplied identifier for this browser session.</summary>
		public string ClientID { get; set; }

		/// <summary>Base64 client public key for session lookup across connections.</summary>
		public string ClientPublicKeyB64 { get; private set; }

		/// <summary>Whether the key exchange has been completed.</summary>
		public bool KeyExchangeDone { get; private set; }

		/// <summary>
		/// Association pairs stored during the session, keyed by association ID.
		/// Each value is the identification public key (base64).
		/// </summary>
		public Dictionary<string, string> Associations { get; }
			= new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Server's base64-encoded public key.</summary>
		public string ServerPublicKeyB64 => _crypto.ServerPublicKeyB64;

		/// <summary>
		/// Completes the key exchange by setting the client's public key.
		/// </summary>
		public void SetClientPublicKey(string clientPublicKeyB64)
		{
			_crypto.SetClientPublicKey(clientPublicKeyB64);
			ClientPublicKeyB64 = clientPublicKeyB64;
			KeyExchangeDone = true;
		}

		/// <summary>
		/// Decrypts an incoming message. Returns null if decryption fails.
		/// </summary>
		public string Decrypt(string ciphertextB64, string nonceB64)
		{
			return _crypto.Decrypt(ciphertextB64, nonceB64);
		}

		/// <summary>
		/// Encrypts a plaintext response with the incremented nonce.
		/// </summary>
		public string Encrypt(string plaintext, string nonceB64)
		{
			return _crypto.Encrypt(plaintext, nonceB64);
		}

		public void Dispose()
		{
			_crypto.Dispose();
		}
	}
}
