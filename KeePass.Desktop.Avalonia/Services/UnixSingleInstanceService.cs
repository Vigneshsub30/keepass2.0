using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using KeePass.Core.Services;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Single-instance enforcement for macOS/Linux using a lock file for
	/// detection and a Unix domain socket for forwarding file paths.
	/// </summary>
	public sealed class UnixSingleInstanceService : ISingleInstanceService
	{
		private readonly string _lockFilePath;
		private readonly string _socketPath;

		private FileStream? _lockFile;
		private CancellationTokenSource? _listenerCts;

		public bool IsFirstInstance { get; }

		public UnixSingleInstanceService()
		{
			string runDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				".local", "run", "keepass");
			Directory.CreateDirectory(runDir);

			_lockFilePath = Path.Combine(runDir, "keepass.lock");
			_socketPath   = Path.Combine(runDir, "keepass.sock");

			IsFirstInstance = TryAcquireLock();
		}

		private bool TryAcquireLock()
		{
			try
			{
				_lockFile = new FileStream(
					_lockFilePath,
					FileMode.OpenOrCreate,
					FileAccess.ReadWrite,
					FileShare.None);  // exclusive — fails if another process holds it
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		}

		public void StartListening(Action<string> onFileReceived)
		{
			if (!IsFirstInstance)
				throw new InvalidOperationException("Only the first instance may listen.");

			// Remove stale socket from a previous run
			if (File.Exists(_socketPath)) File.Delete(_socketPath);

			_listenerCts = new CancellationTokenSource();
			var ct = _listenerCts.Token;

			Task.Run(async () =>
			{
				using var server = new Socket(
					AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				server.Bind(new UnixDomainSocketEndPoint(_socketPath));
				server.Listen(5);
				server.Blocking = false;

				while (!ct.IsCancellationRequested)
				{
					try
					{
						Socket client = await Task.Run(
							() => server.Accept(), ct).ConfigureAwait(false);

						_ = HandleClientAsync(client, onFileReceived, ct);
					}
					catch (OperationCanceledException) { break; }
					catch { /* ignore transient errors */ }
				}
			}, ct);
		}

		private static async Task HandleClientAsync(
			Socket socket, Action<string> onFileReceived, CancellationToken ct)
		{
			try
			{
				using var stream = new NetworkStream(socket, ownsSocket: true);
				using var reader = new StreamReader(stream, Encoding.UTF8);
				string? path = await reader.ReadLineAsync(ct).ConfigureAwait(false);
				if (!string.IsNullOrEmpty(path))
					onFileReceived(path);
			}
			catch { /* ignore */ }
		}

		public bool TrySendToRunningInstance(string filePath)
		{
			if (!File.Exists(_socketPath)) return false;

			try
			{
				using var socket = new Socket(
					AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				socket.Connect(new UnixDomainSocketEndPoint(_socketPath));

				using var stream = new NetworkStream(socket, ownsSocket: false);
				using var writer = new StreamWriter(stream, Encoding.UTF8)
					{ AutoFlush = true };
				writer.WriteLine(filePath);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public void Dispose()
		{
			_listenerCts?.Cancel();
			_listenerCts?.Dispose();
			_lockFile?.Dispose();

			try { File.Delete(_socketPath); } catch { /* best effort */ }
		}
	}
}
