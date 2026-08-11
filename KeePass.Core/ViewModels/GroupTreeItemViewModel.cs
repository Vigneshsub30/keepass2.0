using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using KeePassLib;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// Hierarchical view-model for a single node in the group tree.
	/// Wraps a <see cref="PwGroup"/> and provides lazy-loaded
	/// <see cref="Children"/> so the Avalonia TreeView can build the hierarchy
	/// on demand without loading all groups into memory.
	/// </summary>
	public sealed class GroupTreeItemViewModel : ObservableObject
	{
		private readonly PwGroup _group;

		// ------------------------------------------------------------------ //
		// Properties                                                           //
		// ------------------------------------------------------------------ //

		public string Name => _group.Name;

		public PwIcon IconIndex => _group.IconId;

		public PwUuid Uuid => _group.Uuid;

		private bool _isExpanded;
		public bool IsExpanded
		{
			get => _isExpanded;
			set => SetProperty(ref _isExpanded, value);
		}

		private bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		/// <summary>The underlying domain group (internal; for host VM use only).</summary>
		internal PwGroup Group => _group;

		// ------------------------------------------------------------------ //
		// Children (lazy-loaded)                                               //
		// ------------------------------------------------------------------ //

		private ObservableCollection<GroupTreeItemViewModel>? _children;

		/// <summary>
		/// Child group view-models. Populated on first access (lazy).
		/// </summary>
		public ObservableCollection<GroupTreeItemViewModel> Children
		{
			get
			{
				if (_children == null)
					_children = BuildChildren();
				return _children;
			}
		}

		/// <summary>True when the underlying group has at least one child group.</summary>
		public bool HasChildren => _group.Groups.UCount > 0;

		// ------------------------------------------------------------------ //
		// Constructor                                                          //
		// ------------------------------------------------------------------ //

		public GroupTreeItemViewModel(PwGroup group)
		{
			_group = group ?? throw new ArgumentNullException(nameof(group));
			_isExpanded = group.IsExpanded;
		}

		// ------------------------------------------------------------------ //
		// Helpers                                                              //
		// ------------------------------------------------------------------ //

		private ObservableCollection<GroupTreeItemViewModel> BuildChildren()
		{
			var list = new ObservableCollection<GroupTreeItemViewModel>();
			foreach (var child in _group.Groups)
				list.Add(new GroupTreeItemViewModel(child));
			return list;
		}

		/// <summary>Refreshes the name and icon properties after a rename.</summary>
		public void Refresh()
		{
			OnPropertyChanged(nameof(Name));
			OnPropertyChanged(nameof(IconIndex));
		}
	}
}
