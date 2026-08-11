using System;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Ensures only one instance of the application runs at a time and
	/// forwards file-open requests to the running instance.
	/// </summary>
	public interface ISingleInstanceService : IDisposable
	{
		/// <summary>
		/// Returns <see langword="true"/> when this is the first instance.
		/// Returns <see langword="false"/> when another instance is already running.
		/// </summary>
		bool IsFirstInstance { get; }

		/// <summary>
		/// Start listening for file-open requests forwarded from subsequent
		/// instances.  Call this only when <see cref="IsFirstInstance"/> is
		/// <see langword="true"/>.
		/// </summary>
		/// <param name="onFileReceived">
		/// Callback invoked on the background listener thread for each path
		/// received.  Implementations must marshal to the UI thread if needed.
		/// </param>
		void StartListening(Action<string> onFileReceived);

		/// <summary>
		/// Send <paramref name="filePath"/> to the already-running instance.
		/// Returns <see langword="true"/> on success.
		/// Call this only when <see cref="IsFirstInstance"/> is
		/// <see langword="false"/>.
		/// </summary>
		bool TrySendToRunningInstance(string filePath);
	}
}
