using System;

using CommunityToolkit.Mvvm.ComponentModel;

using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for a single tab in the multi-document tab bar.
	/// Wraps a <see cref="PwDatabase"/> descriptor provided by
	/// <see cref="DatabaseSummaryDto"/> and exposes the tab display state.
	/// </summary>
	public sealed class DatabaseTabViewModel : ObservableObject
	{
		private string _title;
		public string Title
		{
			get => _title;
			private set => SetProperty(ref _title, value ?? string.Empty);
		}

		private bool _isModified;
		public bool IsModified
		{
			get => _isModified;
			set => SetProperty(ref _isModified, value);
		}

		private bool _isLocked;
		public bool IsLocked
		{
			get => _isLocked;
			set => SetProperty(ref _isLocked, value);
		}

		private string _filePath;
		public string FilePath
		{
			get => _filePath;
			private set => SetProperty(ref _filePath, value ?? string.Empty);
		}

		/// <summary>Index of this tab in the session document list.</summary>
		public int DocumentIndex { get; }

		public DatabaseTabViewModel(DatabaseSummaryDto dto, int documentIndex)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			DocumentIndex = documentIndex;
			_title = dto.Name;
			_filePath = dto.Path;
			_isModified = dto.IsModified;
			_isLocked = dto.IsLocked;
		}

		/// <summary>Updates the tab from a refreshed <see cref="DatabaseSummaryDto"/>.</summary>
		public void Update(DatabaseSummaryDto dto)
		{
			if (dto == null) throw new ArgumentNullException(nameof(dto));

			Title = dto.Name;
			FilePath = dto.Path;
			IsModified = dto.IsModified;
			IsLocked = dto.IsLocked;
		}

		/// <summary>Text displayed in the tab header.</summary>
		public string TabHeader =>
			_isModified ? $"{_title} *" : _title;
	}
}
