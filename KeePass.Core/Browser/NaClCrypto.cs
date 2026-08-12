using System;
using System.Security.Cryptography;
using System.Text;

using NaCl;

namespace KeePass.Core.Browser
{
	/// <summary>
	/// Wraps NaCl crypto_box (Curve25519-XSalsa20-Poly1305) for the
	/// KeePassXC-Browser protocol.  Each instance holds a server key pair
	/// and, once a client public key is received, an open crypto_box for
	/// encrypt/decrypt.
	/// </summary>
	public sealed class NaClCrypto : IDisposable
	{
		public const int NonceLength = 24;
		public const int PublicKeyLength = 32;
		public const int SecretKeyLength = 32;

		private readonly byte[] _serverSecretKey;
		private readonly byte[] _serverPublicKey;
		private readonly bool _ownsKeys;
		private Curve25519XSalsa20Poly1305 _box;
		private bool _disposed;

		public NaClCrypto()
		{
			Curve25519XSalsa20Poly1305.KeyPair(
				out _serverSecretKey, out _serverPublicKey);
			_ownsKeys = true;
		}

		/// <summary>
		/// Creates a crypto instance that reuses an existing server key pair.
		/// The caller retains ownership of the key material.
		/// </summary>
		public NaClCrypto(byte[] serverSecretKey, byte[] serverPublicKey)
		{
			if (serverSecretKey == null || serverSecretKey.Length != SecretKeyLength)
				throw new ArgumentException("Secret key must be 32 bytes.");
			if (serverPublicKey == null || serverPublicKey.Length != PublicKeyLength)
				throw new ArgumentException("Public key must be 32 bytes.");

			_serverSecretKey = serverSecretKey;
			_serverPublicKey = serverPublicKey;
			_ownsKeys = false;
		}

		/// <summary>Server public key, base64-encoded.</summary>
		public string ServerPublicKeyB64 => Convert.ToBase64String(_serverPublicKey);

		/// <summary>Raw server public key bytes.</summary>
		public byte[] ServerPublicKey => _serverPublicKey;

		/// <summary>
		/// Sets the client public key and creates the crypto_box used
		/// for all subsequent encrypt/decrypt calls.
		/// </summary>
		public void SetClientPublicKey(byte[] clientPublicKey)
		{
			if (clientPublicKey == null || clientPublicKey.Length != PublicKeyLength)
				throw new ArgumentException("Client public key must be 32 bytes.");

			_box?.Dispose();
			_box = new Curve25519XSalsa20Poly1305(_serverSecretKey, clientPublicKey);
		}

		/// <summary>
		/// Sets the client public key from a base64-encoded string.
		/// </summary>
		public void SetClientPublicKey(string clientPublicKeyB64)
		{
			SetClientPublicKey(Convert.FromBase64String(clientPublicKeyB64));
		}

		/// <summary>Generates a random 24-byte nonce, returned as base64.</summary>
		public static string GenerateNonce()
		{
			byte[] nonce = new byte[NonceLength];
			using (var rng = RandomNumberGenerator.Create())
				rng.GetBytes(nonce);
			return Convert.ToBase64String(nonce);
		}

		/// <summary>
		/// Increments a 24-byte nonce (little-endian) as KeePassXC does for responses.
		/// </summary>
		public static string IncrementNonce(string nonceB64)
		{
			byte[] nonce = Convert.FromBase64String(nonceB64);
			if (nonce.Length != NonceLength)
				throw new ArgumentException("Nonce must be 24 bytes.");

			for (int i = 0; i < nonce.Length; i++)
			{
				if (++nonce[i] != 0)
					break;
			}
			return Convert.ToBase64String(nonce);
		}

		/// <summary>
		/// Encrypts a UTF-8 message with the established crypto_box.
		/// Returns base64-encoded ciphertext.
		/// </summary>
		public string Encrypt(string plaintext, string nonceB64)
		{
			if (_box == null) throw new InvalidOperationException("Client public key not set.");

			byte[] message = Encoding.UTF8.GetBytes(plaintext);
			byte[] nonce = Convert.FromBase64String(nonceB64);
			byte[] cipher = new byte[message.Length + Curve25519XSalsa20Poly1305.TagLength];

			_box.Encrypt(cipher, message, nonce);
			return Convert.ToBase64String(cipher);
		}

		/// <summary>
		/// Decrypts a base64-encoded ciphertext with the established crypto_box.
		/// Returns the UTF-8 plaintext, or null if authentication fails.
		/// </summary>
		public string Decrypt(string ciphertextB64, string nonceB64)
		{
			if (_box == null) throw new InvalidOperationException("Client public key not set.");

			byte[] cipher = Convert.FromBase64String(ciphertextB64);
			byte[] nonce = Convert.FromBase64String(nonceB64);
			byte[] plain = new byte[cipher.Length - Curve25519XSalsa20Poly1305.TagLength];

			if (!_box.TryDecrypt(plain, cipher, nonce))
				return null;

			return Encoding.UTF8.GetString(plain);
		}

		public byte[] ServerSecretKey => _serverSecretKey;

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_box?.Dispose();
			if (_ownsKeys)
				Array.Clear(_serverSecretKey, 0, _serverSecretKey.Length);
		}
	}
}
