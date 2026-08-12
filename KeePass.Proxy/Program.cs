using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeePass.Proxy
{
	/// <summary>
	/// Native Messaging proxy that bridges browser extension stdin/stdout
	/// with the KeePass app's Unix domain socket / named pipe.
	///
	/// The browser launches this process via Native Messaging.  Messages
	/// are framed with a 4-byte little-endian length prefix on both sides.
	/// </summary>
	internal static class Program
	{
		private static readonly string LogPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			".local", "run", "keepass", "proxy.log");

		private static void Log(string msg)
		{
			try
			{
				File.AppendAllText(LogPath,
					$"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
			}
			catch { /* best effort */ }
		}

		private static async Task<int> Main(string[] args)
		{
			Log($"Proxy started. Args: {string.Join(" ", args)}");
			string socketPath = GetSocketPath();
			Log($"Socket path: {socketPath}");

			using var socket = new Socket(
				AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

			try
			{
				socket.Connect(new UnixDomainSocketEndPoint(socketPath));
				Log("Connected to socket");
			}
			catch (SocketException ex)
			{
				Log($"Socket connect failed: {ex.Message}");
				WriteErrorToStderr("Cannot connect to KeePass — is it running?");
				return 1;
			}

			using var socketStream = new NetworkStream(socket, ownsSocket: false);
			var stdin = Console.OpenStandardInput();
			var stdout = Console.OpenStandardOutput();

			try
			{
				int msgCount = 0;
				while (true)
				{
					byte[] message = await ReadNativeMessageAsync(stdin).ConfigureAwait(false);
					if (message == null)
					{
						Log("stdin EOF");
						break;
					}

					msgCount++;
					string msgText = Encoding.UTF8.GetString(message);
					Log($"MSG#{msgCount} FROM_CHROME ({message.Length}b): {(msgText.Length > 300 ? msgText.Substring(0, 300) + "..." : msgText)}");

					await WriteFrameAsync(socketStream, message).ConfigureAwait(false);
					Log($"MSG#{msgCount} forwarded to socket");

					byte[] response = await ReadFrameAsync(socketStream).ConfigureAwait(false);
					if (response == null)
					{
						Log("Socket EOF");
						break;
					}

					string respText = Encoding.UTF8.GetString(response);
					Log($"MSG#{msgCount} FROM_SOCKET ({response.Length}b): {(respText.Length > 300 ? respText.Substring(0, 300) + "..." : respText)}");

					await WriteNativeMessageAsync(stdout, response).ConfigureAwait(false);
					Log($"MSG#{msgCount} sent to Chrome");
				}
			}
			catch (Exception ex)
			{
				Log($"Error: {ex.GetType().Name}: {ex.Message}");
			}

			Log("Proxy exiting");
			return 0;
		}

		/// <summary>
		/// Reads a Native Messaging frame from stdin:
		/// 4 bytes LE length + UTF-8 JSON payload.
		/// </summary>
		private static async Task<byte[]> ReadNativeMessageAsync(Stream stdin)
		{
			byte[] lengthBuf = new byte[4];
			int totalRead = 0;
			while (totalRead < 4)
			{
				int n = await stdin.ReadAsync(lengthBuf, totalRead, 4 - totalRead)
					.ConfigureAwait(false);
				if (n == 0) return null;
				totalRead += n;
			}

			uint length = BitConverter.ToUInt32(lengthBuf, 0);
			if (length == 0 || length > 16 * 1024 * 1024)
				return null;

			byte[] payload = new byte[length];
			totalRead = 0;
			while (totalRead < (int)length)
			{
				int n = await stdin.ReadAsync(payload, totalRead, (int)length - totalRead)
					.ConfigureAwait(false);
				if (n == 0) return null;
				totalRead += n;
			}

			return payload;
		}

		/// <summary>
		/// Writes a Native Messaging frame to stdout:
		/// 4 bytes LE length + payload.
		/// </summary>
		private static async Task WriteNativeMessageAsync(Stream stdout, byte[] payload)
		{
			byte[] lengthBuf = BitConverter.GetBytes((uint)payload.Length);
			await stdout.WriteAsync(lengthBuf, 0, 4).ConfigureAwait(false);
			await stdout.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
			await stdout.FlushAsync().ConfigureAwait(false);
		}

		/// <summary>
		/// Reads a length-prefixed frame from the KeePass socket.
		/// </summary>
		private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
		{
			byte[] lengthBuf = new byte[4];
			int totalRead = 0;
			while (totalRead < 4)
			{
				int n = await stream.ReadAsync(lengthBuf, totalRead, 4 - totalRead)
					.ConfigureAwait(false);
				if (n == 0) return null;
				totalRead += n;
			}

			uint length = BitConverter.ToUInt32(lengthBuf, 0);
			if (length == 0 || length > 16 * 1024 * 1024)
				return null;

			byte[] payload = new byte[length];
			totalRead = 0;
			while (totalRead < (int)length)
			{
				int n = await stream.ReadAsync(payload, totalRead, (int)length - totalRead)
					.ConfigureAwait(false);
				if (n == 0) return null;
				totalRead += n;
			}

			return payload;
		}

		/// <summary>
		/// Writes a length-prefixed frame to the KeePass socket.
		/// </summary>
		private static async Task WriteFrameAsync(NetworkStream stream, byte[] payload)
		{
			byte[] lengthBuf = BitConverter.GetBytes((uint)payload.Length);
			await stream.WriteAsync(lengthBuf, 0, 4).ConfigureAwait(false);
			await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
			await stream.FlushAsync().ConfigureAwait(false);
		}

		private static string GetSocketPath()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"KeePass", "keepass_browser.sock");
			}

			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				".local", "run", "keepass",
				"org.keepassxc.KeePassXC.BrowserServer");
		}

		private static void WriteErrorToStderr(string message)
		{
			Console.Error.WriteLine("[keepass-proxy] " + message);
		}
	}
}
