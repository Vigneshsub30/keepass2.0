using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using KeePass.Core.Services;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Single-instance enforcement for Windows using a named Mutex for
	/// detection and a named pipe for forwarding file paths to the first
	/// instance.
	/// </summary>
	public sealed class WindowsSingleInstanceService : ISingleInstanceService
	{
		private const string MutexName = "Global\\KeePass_SingleInstance_Mutex";
		private const string PipeName  = "KeePass_SingleInstance_Pipe";
		private const int    PipeTimeout = 5_000; // ms

		private readonly Mutex _mutex;
		private CancellationTokenSource? _listenerCts;

		public bool IsFirstInstance { get; }

		public WindowsSingleInstanceService()
		{
			_mutex          = new Mutex(initiallyOwned: false, MutexName, out bool createdNew);
			IsFirstInstance = createdNew;
		}

		public void StartListening(Action<string> onFileReceived)
		{
			if (!IsFirstInstance)
				throw new InvalidOperationException("Only the first instance may listen.");

			_listenerCts = new CancellationTokenSource();
			var ct = _listenerCts.Token;

			Task.Run(async () =>
			{
				while (!ct.IsCancellationRequested)
				{
					try
					{
						using var server = new NamedPipeServerStream(
							PipeName,
							PipeDirection.In,
							maxNumberOfServerInstances: 1,
							transmissionMode: PipeTransmissionMode.Byte,
							options: PipeOptions.Asynchronous);

						await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

						using var reader = new StreamReader(server, Encoding.UTF8);
						string? path = await reader.ReadLineAsync(ct).ConfigureAwait(false);
						if (!string.IsNullOrEmpty(path))
							onFileReceived(path);
					}
					catch (OperationCanceledException) { break; }
					catch { /* ignore transient pipe errors */ }
				}
			}, ct);
		}

		public bool TrySendToRunningInstance(string filePath)
		{
			try
			{
				using var client = new NamedPipeClientStream(
					".", PipeName, PipeDirection.Out);
				client.Connect(PipeTimeout);

				using var writer = new StreamWriter(client, Encoding.UTF8)
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
			_mutex.Dispose();
		}
	}
}
