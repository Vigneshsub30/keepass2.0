using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Platform-neutral abstraction for native file open/save dialogs.
	/// </summary>
	public interface IFileDialogService
	{
		/// <summary>
		/// Presents a platform-native open-file dialog.
		/// </summary>
		/// <param name="title">Dialog title text.</param>
		/// <param name="filters">
		/// Ordered list of extension filters displayed in the dialog.
		/// </param>
		/// <returns>
		/// The absolute path chosen by the user, or <c>null</c> when the
		/// dialog is cancelled.
		/// </returns>
		Task<string?> OpenFileAsync(string title, IReadOnlyList<FileDialogFilter> filters);
	}

	/// <summary>
	/// Describes a single file-type entry shown in an open/save dialog.
	/// </summary>
	public sealed class FileDialogFilter
	{
		/// <summary>Human-readable filter label, e.g. "Key Files".</summary>
		public string Name { get; init; } = string.Empty;

		/// <summary>
		/// Glob patterns without wildcards, e.g. <c>{ "keyx", "key" }</c>.
		/// </summary>
		public IReadOnlyList<string> Extensions { get; init; }
			= System.Array.Empty<string>();
	}
}
