using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using KeePass.Core.Browser;
using KeePass.Core.Services;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Unix domain socket (macOS/Linux) or named pipe (Windows) server
	/// that accepts connections from the keepass-proxy native messaging
	/// relay.  Each connected browser gets its own <see cref="BrowserSession"/>.
	/// </summary>
	internal sealed class BrowserSocketServer : IDisposable
	{
		private readonly IDatabaseSessionService _session;
		private readonly BrowserAction _action;
		private readonly string _socketPath;

		private CancellationTokenSource _cts;
		private Socket _listener;
		private readonly ConcurrentDictionary<string, BrowserSession> _sessions
			= new ConcurrentDictionary<string, BrowserSession>();
		private readonly ConcurrentDictionary<int, Socket> _clientSockets
			= new ConcurrentDictionary<int, Socket>();
		private int _clientIdCounter;

		/// <summary>
		/// Shared server key pair so the same public key is returned across
		/// all connections.  The extension may use one-shot sendNativeMessage
		/// (each message = new proxy = new socket connection), so the server
		/// key must be stable across connections.
		/// </summary>
		private readonly NaClCrypto _sharedServerKeys = new NaClCrypto();

		public BrowserSocketServer(IDatabaseSessionService session)
		{
			_session = session ?? throw new ArgumentNullException(nameof(session));
			_action = new BrowserAction(session);
			_socketPath = GetSocketPath();
		}

		/// <summary>Well-known socket path for the running app.</summary>
		public string SocketPath => _socketPath;

		/// <summary>Whether the server is currently listening.</summary>
		public bool IsListening => _listener != null;

		/// <summary>Starts listening for browser connections.</summary>
		public void Start()
		{
			if (_listener != null) return;

			string dir = Path.GetDirectoryName(_socketPath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			if (File.Exists(_socketPath))
				File.Delete(_socketPath);

			_cts = new CancellationTokenSource();
			_listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			_listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
			_listener.Listen(5);

			Task.Run(() => AcceptLoop(_cts.Token));
		}

		/// <summary>Stops the server and cleans up all sessions.</summary>
		public void Stop()
		{
			_cts?.Cancel();

			foreach (var kvp in _clientSockets)
			{
				try { kvp.Value.Close(); } catch { /* best effort */ }
			}
			_clientSockets.Clear();

			try { _listener?.Close(); } catch { /* best effort */ }
			_listener = null;

			foreach (var kvp in _sessions)
				kvp.Value.Dispose();
			_sessions.Clear();
			_sharedServerKeys.Dispose();

			try { if (File.Exists(_socketPath)) File.Delete(_socketPath); }
			catch { /* best effort */ }
		}

		public void Dispose() => Stop();

		private async Task AcceptLoop(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					Socket client = await _listener.AcceptAsync(ct)
						.ConfigureAwait(false);
					_ = HandleClientAsync(client, ct);
				}
				catch (OperationCanceledException) { break; }
				catch (SocketException) when (ct.IsCancellationRequested) { break; }
				catch (ObjectDisposedException) { break; }
				catch { /* transient errors — keep accepting */ }
			}
		}

		private async Task HandleClientAsync(Socket client, CancellationToken ct)
		{
			int id = Interlocked.Increment(ref _clientIdCounter);
			_clientSockets[id] = client;
			try
			{
				using var stream = new NetworkStream(client, ownsSocket: true);

				while (!ct.IsCancellationRequested)
				{
					string request = await ReadFrameAsync(stream, ct).ConfigureAwait(false);
					if (request == null) break;

					BrowserSession session = ResolveSession(request);

					string response = _action.ProcessMessage(request, session);
					await WriteFrameAsync(stream, response, ct).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) { /* shutdown */ }
			catch (IOException) { /* client disconnected */ }
			catch (SocketException) { /* client disconnected */ }
			catch { /* unexpected — log in future */ }
			finally
			{
				_clientSockets.TryRemove(id, out _);
			}
		}

		/// <summary>
		/// Finds or creates a session for the request.  Sessions are keyed by
		/// client public key so they survive proxy restarts (one-shot
		/// sendNativeMessage mode).  All sessions share the server's key pair.
		/// </summary>
		private BrowserSession ResolveSession(string json)
		{
			string clientPubKey = ExtractField(json, "publicKey");
			string clientId = ExtractField(json, "clientID");

			if (!string.IsNullOrEmpty(clientPubKey)
				&& _sessions.TryGetValue(clientPubKey, out var byKey))
			{
				if (!string.IsNullOrEmpty(clientId))
					_sessions[clientId] = byKey;
				return byKey;
			}

			if (!string.IsNullOrEmpty(clientId)
				&& _sessions.TryGetValue(clientId, out var byId))
				return byId;

			var session = new BrowserSession(_sharedServerKeys);

			if (!string.IsNullOrEmpty(clientPubKey))
				_sessions[clientPubKey] = session;
			if (!string.IsNullOrEmpty(clientId))
				_sessions[clientId] = session;

			return session;
		}

		/// <summary>
		/// Reads a length-prefixed frame: 4 bytes (little-endian uint32) + UTF-8 JSON.
		/// </summary>
		private static async Task<string> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
		{
			byte[] lengthBuf = new byte[4];
			int read = 0;
			while (read < 4)
			{
				int n = await stream.ReadAsync(lengthBuf, read, 4 - read, ct).ConfigureAwait(false);
				if (n == 0) return null;
				read += n;
			}

			uint length = BitConverter.ToUInt32(lengthBuf, 0);
			if (length == 0 || length > 16 * 1024 * 1024)
				return null;

			byte[] messageBuf = new byte[length];
			read = 0;
			while (read < (int)length)
			{
				int n = await stream.ReadAsync(messageBuf, read, (int)length - read, ct)
					.ConfigureAwait(false);
				if (n == 0) return null;
				read += n;
			}

			return Encoding.UTF8.GetString(messageBuf);
		}

		/// <summary>
		/// Writes a length-prefixed frame: 4 bytes (little-endian uint32) + UTF-8 JSON.
		/// </summary>
		private static async Task WriteFrameAsync(NetworkStream stream, string json, CancellationToken ct)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			byte[] length = BitConverter.GetBytes((uint)payload.Length);

			await stream.WriteAsync(length, 0, 4, ct).ConfigureAwait(false);
			await stream.WriteAsync(payload, 0, payload.Length, ct).ConfigureAwait(false);
			await stream.FlushAsync(ct).ConfigureAwait(false);
		}

		private static string ExtractField(string json, string fieldName)
		{
			try
			{
				using var doc = System.Text.Json.JsonDocument.Parse(json);
				if (doc.RootElement.TryGetProperty(fieldName, out var prop))
					return prop.GetString();
			}
			catch { /* ignore parse errors */ }
			return null;
		}

		private static string GetSocketPath()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"KeePass", "keepass_browser.sock");
			}

			string runDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				".local", "run", "keepass");

			return Path.Combine(runDir, "org.keepassxc.KeePassXC.BrowserServer");
		}
	}
}
