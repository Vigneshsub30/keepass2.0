using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using KeePass.Core.Services;

using CoreFilter = KeePass.Core.Services.FileDialogFilter;

namespace KeePass.Desktop.Avalonia.Services
{
	/// <summary>
	/// Wraps the Avalonia <see cref="IStorageProvider"/> to implement
	/// <see cref="IFileDialogService"/> for the real desktop application.
	/// Requires a reference to the current <see cref="Window"/> at construction
	/// time to access <c>StorageProvider</c>.
	/// </summary>
	internal sealed class AvaloniaFileDialogService : IFileDialogService
	{
		private readonly Func<Window?> _windowFactory;

		/// <param name="windowFactory">
		/// Delegate that returns the current top-level window. Using a factory
		/// rather than a direct reference avoids capturing the window before it
		/// is shown.
		/// </param>
		public AvaloniaFileDialogService(Func<Window?> windowFactory)
		{
			_windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
		}

		public async Task<string?> OpenFileAsync(
			string title,
			IReadOnlyList<CoreFilter> filters)
		{
			Window? window = _windowFactory();
			if (window == null) return null;

			IStorageProvider storage = window.StorageProvider;

			var fileTypes = filters
				.Select(f => new FilePickerFileType(f.Name)
				{
					Patterns = f.Extensions.Select(e => e == "*" ? "*.*" : $"*.{e}").ToList()
				})
				.ToList();

			var options = new FilePickerOpenOptions
			{
				Title = title,
				AllowMultiple = false,
				FileTypeFilter = fileTypes
			};

			var result = await storage.OpenFilePickerAsync(options);
			return result?.Count > 0 ? result[0].TryGetLocalPath() : null;
		}

		public async Task<string?> SaveFileAsync(
			string title,
			IReadOnlyList<CoreFilter> filters,
			string? defaultFileName = null)
		{
			Window? window = _windowFactory();
			if (window == null) return null;

			IStorageProvider storage = window.StorageProvider;

			var fileTypes = filters
				.Select(f => new FilePickerFileType(f.Name)
				{
					Patterns = f.Extensions.Select(e => e == "*" ? "*.*" : $"*.{e}").ToList()
				})
				.ToList();

			var options = new FilePickerSaveOptions
			{
				Title = title,
				FileTypeChoices = fileTypes,
				SuggestedFileName = defaultFileName
			};

			var result = await storage.SaveFilePickerAsync(options);
			return result?.TryGetLocalPath();
		}
	}
}
