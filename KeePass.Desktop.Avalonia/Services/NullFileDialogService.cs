using System.Collections.Generic;
using System.Threading.Tasks;

using KeePass.Core.Services;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// No-op implementation of <see cref="IFileDialogService"/> used during
	/// headless testing and in the DI container when no real window is available.
	/// Always returns <c>null</c> (equivalent to the user cancelling the dialog).
	/// </summary>
	internal sealed class NullFileDialogService : IFileDialogService
	{
		public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileDialogFilter> filters)
			=> Task.FromResult<string?>(null);

		public Task<string?> SaveFileAsync(string title, IReadOnlyList<FileDialogFilter> filters, string? defaultFileName = null)
			=> Task.FromResult<string?>(null);
	}
}
