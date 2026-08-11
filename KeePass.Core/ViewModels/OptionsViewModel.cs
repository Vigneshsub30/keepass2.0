#nullable enable

using System;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KeePass.Core.ViewModels
{
	// ======================================================================
	// Security settings sub-VM
	// ======================================================================

	/// <summary>
	/// Observable settings for the Security tab of the Options dialog.
	/// Maps to KeePass <c>AceSecurity</c> properties.
	/// </summary>
	public sealed class SecurityOptionsViewModel : ObservableObject
	{
		// Lock workspace
		private bool _lockOnWindowMinimize;
		public bool LockOnWindowMinimize
		{
			get => _lockOnWindowMinimize;
			set => SetProperty(ref _lockOnWindowMinimize, value);
		}

		private bool _lockOnSessionSwitch;
		public bool LockOnSessionSwitch
		{
			get => _lockOnSessionSwitch;
			set => SetProperty(ref _lockOnSessionSwitch, value);
		}

		private bool _lockOnSuspend;
		public bool LockOnSuspend
		{
			get => _lockOnSuspend;
			set => SetProperty(ref _lockOnSuspend, value);
		}

		private uint _lockAfterSeconds;
		/// <summary>
		/// Lock workspace after this many seconds of inactivity (0 = disabled).
		/// </summary>
		public uint LockAfterSeconds
		{
			get => _lockAfterSeconds;
			set => SetProperty(ref _lockAfterSeconds, value);
		}

		// Clipboard
		private int _clipboardClearAfterSeconds = 12;
		public int ClipboardClearAfterSeconds
		{
			get => _clipboardClearAfterSeconds;
			set => SetProperty(ref _clipboardClearAfterSeconds, Math.Max(0, value));
		}

		private bool _clipboardClearOnExit;
		public bool ClipboardClearOnExit
		{
			get => _clipboardClearOnExit;
			set => SetProperty(ref _clipboardClearOnExit, value);
		}

		private bool _clipboardNoPersist;
		public bool ClipboardNoPersist
		{
			get => _clipboardNoPersist;
			set => SetProperty(ref _clipboardNoPersist, value);
		}

		// Master key
		private bool _masterKeyOnSecureDesktop;
		public bool MasterKeyOnSecureDesktop
		{
			get => _masterKeyOnSecureDesktop;
			set => SetProperty(ref _masterKeyOnSecureDesktop, value);
		}

		private int _masterKeyTries;
		/// <summary>Maximum number of unlock attempts before giving up (0 = unlimited).</summary>
		public int MasterKeyTries
		{
			get => _masterKeyTries;
			set => SetProperty(ref _masterKeyTries, Math.Max(0, value));
		}

		// Misc security
		private bool _preventScreenCapture;
		public bool PreventScreenCapture
		{
			get => _preventScreenCapture;
			set => SetProperty(ref _preventScreenCapture, value);
		}

		private bool _clearKeyCommandLineParams;
		public bool ClearKeyCommandLineParams
		{
			get => _clearKeyCommandLineParams;
			set => SetProperty(ref _clearKeyCommandLineParams, value);
		}
	}

	// ======================================================================
	// Interface settings sub-VM
	// ======================================================================

	/// <summary>
	/// Observable settings for the Interface/Appearance tab.
	/// Maps to KeePass <c>AceUI</c> properties.
	/// </summary>
	public sealed class InterfaceOptionsViewModel : ObservableObject
	{
		private string _languageFile = string.Empty;
		public string LanguageFile
		{
			get => _languageFile;
			set => SetProperty(ref _languageFile, value ?? string.Empty);
		}

		private bool _showToolBar = true;
		public bool ShowToolBar
		{
			get => _showToolBar;
			set => SetProperty(ref _showToolBar, value);
		}

		private bool _showStatusBar = true;
		public bool ShowStatusBar
		{
			get => _showStatusBar;
			set => SetProperty(ref _showStatusBar, value);
		}

		private bool _minimizeToTray;
		public bool MinimizeToTray
		{
			get => _minimizeToTray;
			set => SetProperty(ref _minimizeToTray, value);
		}

		private bool _closeToTray;
		public bool CloseToTray
		{
			get => _closeToTray;
			set => SetProperty(ref _closeToTray, value);
		}

		private bool _showGridLines;
		public bool ShowGridLines
		{
			get => _showGridLines;
			set => SetProperty(ref _showGridLines, value);
		}

		private int _expirySoonDays = 14;
		public int ExpirySoonDays
		{
			get => _expirySoonDays;
			set => SetProperty(ref _expirySoonDays, Math.Max(0, value));
		}
	}

	// ======================================================================
	// Integration settings sub-VM
	// ======================================================================

	/// <summary>
	/// Observable settings for the Integration tab.
	/// Maps to KeePass <c>AceIntegration</c> properties.
	/// </summary>
	public sealed class IntegrationOptionsViewModel : ObservableObject
	{
		private string _urlOverride = string.Empty;
		/// <summary>
		/// Global URL scheme override (e.g. <c>cmd://...</c>). Empty = default.
		/// </summary>
		public string UrlOverride
		{
			get => _urlOverride;
			set => SetProperty(ref _urlOverride, value ?? string.Empty);
		}

		private bool _autoTypeEnabled = true;
		public bool AutoTypeEnabled
		{
			get => _autoTypeEnabled;
			set => SetProperty(ref _autoTypeEnabled, value);
		}

		private bool _autoTypeMatchByTitle = true;
		public bool AutoTypeMatchByTitle
		{
			get => _autoTypeMatchByTitle;
			set => SetProperty(ref _autoTypeMatchByTitle, value);
		}

		private bool _autoTypeAlwaysShowSelectionDialog;
		public bool AutoTypeAlwaysShowSelectionDialog
		{
			get => _autoTypeAlwaysShowSelectionDialog;
			set => SetProperty(ref _autoTypeAlwaysShowSelectionDialog, value);
		}

		private int _autoTypeDelay = 100;
		/// <summary>Delay in milliseconds between each auto-type keystroke.</summary>
		public int AutoTypeDelay
		{
			get => _autoTypeDelay;
			set => SetProperty(ref _autoTypeDelay, Math.Max(0, Math.Min(30000, value)));
		}
	}

	// ======================================================================
	// Advanced settings sub-VM
	// ======================================================================

	/// <summary>
	/// Observable settings for the Advanced tab.
	/// Maps to KeePass <c>AceApplication</c> / misc properties.
	/// </summary>
	public sealed class AdvancedOptionsViewModel : ObservableObject
	{
		private bool _startMinimized;
		public bool StartMinimized
		{
			get => _startMinimized;
			set => SetProperty(ref _startMinimized, value);
		}

		private bool _openLastFile = true;
		public bool OpenLastFile
		{
			get => _openLastFile;
			set => SetProperty(ref _openLastFile, value);
		}

		private bool _rememberWorkingDirectories = true;
		public bool RememberWorkingDirectories
		{
			get => _rememberWorkingDirectories;
			set => SetProperty(ref _rememberWorkingDirectories, value);
		}

		private bool _autoSaveAfterEntryEdit;
		public bool AutoSaveAfterEntryEdit
		{
			get => _autoSaveAfterEntryEdit;
			set => SetProperty(ref _autoSaveAfterEntryEdit, value);
		}

		private bool _useTransactedFileWrites = true;
		public bool UseTransactedFileWrites
		{
			get => _useTransactedFileWrites;
			set => SetProperty(ref _useTransactedFileWrites, value);
		}

		private bool _verifyWrittenFileAfterSaving = true;
		public bool VerifyWrittenFileAfterSaving
		{
			get => _verifyWrittenFileAfterSaving;
			set => SetProperty(ref _verifyWrittenFileAfterSaving, value);
		}
	}

	// ======================================================================
	// Main options view-model
	// ======================================================================

	/// <summary>
	/// Top-level ViewModel for the Application Options dialog. Aggregates four
	/// category sub-ViewModels and exposes Apply/Cancel commands.
	/// </summary>
	/// <remarks>
	/// The design deliberately keeps this class free of any dependency on
	/// <c>AppConfigEx</c> or <c>AceXxx</c> WinForms classes. The caller
	/// (the Avalonia App startup) is responsible for populating these
	/// sub-ViewModels from <c>AppConfigEx</c> and writing them back on Apply.
	/// This allows the ViewModel to be fully tested without any WinForms
	/// dependencies.
	/// </remarks>
	public sealed class OptionsViewModel : ObservableObject
	{
		// ------------------------------------------------------------------ //
		// Sub-view-models                                                     //
		// ------------------------------------------------------------------ //

		public SecurityOptionsViewModel Security { get; } = new SecurityOptionsViewModel();
		public InterfaceOptionsViewModel Interface { get; } = new InterfaceOptionsViewModel();
		public IntegrationOptionsViewModel Integration { get; } = new IntegrationOptionsViewModel();
		public AdvancedOptionsViewModel Advanced { get; } = new AdvancedOptionsViewModel();

		// ------------------------------------------------------------------ //
		// Policy enforcement                                                  //
		// ------------------------------------------------------------------ //

		/// <summary>
		/// When true, security settings are policy-locked and the corresponding
		/// UI controls should be disabled.
		/// </summary>
		private bool _securityLocked;
		public bool SecurityLocked
		{
			get => _securityLocked;
			set => SetProperty(ref _securityLocked, value);
		}

		/// <summary>
		/// When true, the auto-type integration settings are policy-locked.
		/// </summary>
		private bool _autoTypeLocked;
		public bool AutoTypeLocked
		{
			get => _autoTypeLocked;
			set => SetProperty(ref _autoTypeLocked, value);
		}

		// ------------------------------------------------------------------ //
		// Commands                                                            //
		// ------------------------------------------------------------------ //

		public IRelayCommand ApplyCommand { get; }
		public IRelayCommand CancelCommand { get; }

		/// <summary>Raised when OK/Apply is executed.</summary>
		public event EventHandler? Applied;

		/// <summary>Raised when Cancel is executed.</summary>
		public event EventHandler? Cancelled;

		// ------------------------------------------------------------------ //
		// Constructor                                                         //
		// ------------------------------------------------------------------ //

		public OptionsViewModel()
		{
			ApplyCommand  = new RelayCommand(() => Applied?.Invoke(this, EventArgs.Empty));
			CancelCommand = new RelayCommand(() => Cancelled?.Invoke(this, EventArgs.Empty));
		}
	}
}
