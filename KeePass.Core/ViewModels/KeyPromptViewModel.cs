using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePass.Core.Services;

using KeePassLib.Cryptography;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// View-model for the key-prompt dialog. Handles master-password entry,
	/// key-file selection, and optional Windows user-account key on a
	/// background thread, with real-time password-quality reporting and
	/// progress feedback during key derivation.
	/// </summary>
	public sealed class KeyPromptViewModel : ObservableObject
	{
		private readonly IOConnectionInfo _ioc;
		private readonly IKeyFileLocator _keyFileLocator;
		private readonly IFileDialogService? _fileDialogService;

		private ViewModelProgressLogger? _currentLogger;

		private static readonly IReadOnlyList<FileDialogFilter> KeyFileFilters =
			new[]
			{
				new FileDialogFilter { Name = "Key Files", Extensions = new[] { "keyx", "key" } },
				new FileDialogFilter { Name = "All Files", Extensions = new[] { "*" } }
			};

		// ------------------------------------------------------------------ //
		// Events                                                               //
		// ------------------------------------------------------------------ //

		/// <summary>Raised when key derivation succeeds.</summary>
		public event EventHandler<CompositeKey>? UnlockSucceeded;

		/// <summary>Raised when key derivation fails with a user-facing message.</summary>
		public event EventHandler<string>? UnlockFailed;

		/// <summary>
		/// Raised on successful unlock with the data the caller should persist
		/// as the saved key association for this database.
		/// </summary>
		public event EventHandler<KeyAssociationData>? KeyAssociationChanged;

		// ------------------------------------------------------------------ //
		// Observable properties                                                //
		// ------------------------------------------------------------------ //

		private ProtectedString _masterPassword = ProtectedString.Empty;
		public ProtectedString MasterPassword
		{
			get => _masterPassword;
			set
			{
				if (SetProperty(ref _masterPassword, value ?? ProtectedString.Empty))
				{
					OnPropertyChanged(nameof(PasswordQualityBits));
					UnlockCommand.NotifyCanExecuteChanged();
				}
			}
		}


		private bool _useKeyFile;
		public bool UseKeyFile
		{
			get => _useKeyFile;
			set
			{
				if (SetProperty(ref _useKeyFile, value))
					UnlockCommand.NotifyCanExecuteChanged();
			}
		}

		private string _keyFilePath = string.Empty;
		public string KeyFilePath
		{
			get => _keyFilePath;
			set => SetProperty(ref _keyFilePath, value ?? string.Empty);
		}

		private bool _useUserAccount;
		public bool UseUserAccount
		{
			get => _useUserAccount;
			set
			{
				if (SetProperty(ref _useUserAccount, value))
					UnlockCommand.NotifyCanExecuteChanged();
			}
		}

		private string _databasePath = string.Empty;
		public string DatabasePath
		{
			get => _databasePath;
			set => SetProperty(ref _databasePath, value ?? string.Empty);
		}

		/// <summary>
		/// Password quality in bits, updated in real-time as
		/// <see cref="MasterPassword"/> changes.
		/// Returns 0 when the password is empty.
		/// </summary>
		public uint PasswordQualityBits
		{
			get
			{
				if (_masterPassword.IsEmpty) return 0u;

				byte[] pbUtf8 = _masterPassword.ReadUtf8();
				try
				{
					return QualityEstimation.EstimatePasswordBits(pbUtf8);
				}
				catch
				{
					// QualityEstimation may throw if PopularPasswords is not
					// initialised (unit-test context). Degrade gracefully.
					return 0u;
				}
				finally
				{
					MemUtil.ZeroByteArray(pbUtf8);
				}
			}
		}

		private bool _isKeyDerivationInProgress;
		public bool IsKeyDerivationInProgress
		{
			get => _isKeyDerivationInProgress;
			private set => SetProperty(ref _isKeyDerivationInProgress, value);
		}

		private double _keyDerivationProgress;
		/// <summary>Key-derivation progress in the range [0, 100].</summary>
		public double KeyDerivationProgress
		{
			get => _keyDerivationProgress;
			private set => SetProperty(ref _keyDerivationProgress, value);
		}

		// ------------------------------------------------------------------ //
		// Read-only helpers for data-binding                                   //
		// ------------------------------------------------------------------ //

		/// <summary>Suggested key-file paths returned by <see cref="IKeyFileLocator"/>.</summary>
		public IReadOnlyList<string> SuggestedKeyFiles =>
			_keyFileLocator.GetSuggestedKeyFiles(_ioc);

		// ------------------------------------------------------------------ //
		// Commands                                                             //
		// ------------------------------------------------------------------ //

		public IAsyncRelayCommand UnlockCommand { get; }

	/// <summary>Opens a file dialog and sets <see cref="KeyFilePath"/>.</summary>
	public IAsyncRelayCommand BrowseKeyFileCommand { get; }

		// ------------------------------------------------------------------ //
		// Constructor                                                          //
		// ------------------------------------------------------------------ //

		/// <param name="ioc">Connection info for the database being unlocked.</param>
		/// <param name="keyFileLocator">
		/// Platform-specific locator for suggested key files.
		/// </param>
		/// <param name="fileDialogService">
		/// Optional file dialog service used by <see cref="BrowseKeyFileCommand"/>.
		/// When <c>null</c>, the browse command is a no-op (useful in tests).
		/// </param>
		/// <param name="savedAssociation">
		/// Optional previously-saved key association that pre-populates the VM.
		/// </param>
		public KeyPromptViewModel(
			IOConnectionInfo ioc,
			IKeyFileLocator keyFileLocator,
			IFileDialogService? fileDialogService = null,
			KeyAssociationData? savedAssociation = null)
		{
			_ioc = ioc ?? throw new ArgumentNullException(nameof(ioc));
			_keyFileLocator = keyFileLocator ?? throw new ArgumentNullException(nameof(keyFileLocator));
			_fileDialogService = fileDialogService;

			UnlockCommand = new AsyncRelayCommand(ExecuteUnlockAsync, CanExecuteUnlock);
			BrowseKeyFileCommand = new AsyncRelayCommand(ExecuteBrowseKeyFileAsync);

			ApplySavedAssociation(savedAssociation);
		}

		// ------------------------------------------------------------------ //
		// Command implementation                                               //
		// ------------------------------------------------------------------ //

		private bool CanExecuteUnlock()
		{
			if (_isKeyDerivationInProgress) return false;

			bool hasPassword = !_masterPassword.IsEmpty;
			bool hasKeyFile = _useKeyFile && !string.IsNullOrWhiteSpace(_keyFilePath);
			bool hasUserAccount = _useUserAccount;

			return hasPassword || hasKeyFile || hasUserAccount;
		}

		private async Task ExecuteUnlockAsync()
		{
			IsKeyDerivationInProgress = true;
			KeyDerivationProgress = 0;
			UnlockCommand.NotifyCanExecuteChanged();

			// Capture values before switching to background thread.
			byte[]? pbPasswordUtf8 = _masterPassword.IsEmpty
				? null
				: _masterPassword.ReadUtf8();

			bool useKeyFile = _useKeyFile;
			string keyFilePath = _keyFilePath;
			bool useUserAccount = _useUserAccount;
			IOConnectionInfo ioc = _ioc;

			var logger = new ViewModelProgressLogger(pct => KeyDerivationProgress = pct);
			_currentLogger = logger;

			CompositeKey? ck = null;
			string? errorMessage = null;

			try
			{
				ck = await Task.Run(() => BuildKey(
					pbPasswordUtf8, useKeyFile, keyFilePath, useUserAccount, ioc, logger));
			}
			catch (OperationCanceledException)
			{
				errorMessage = "Key derivation was cancelled.";
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;
			}
			finally
			{
				if (pbPasswordUtf8 != null) MemUtil.ZeroByteArray(pbPasswordUtf8);
				_currentLogger = null;
				IsKeyDerivationInProgress = false;
				KeyDerivationProgress = 0;
				UnlockCommand.NotifyCanExecuteChanged();
			}

			if (ck == null && errorMessage == null)
				errorMessage = "No key sources provided.";

			if (errorMessage != null)
			{
				UnlockFailed?.Invoke(this, errorMessage);
				return;
			}

			KeyAssociationChanged?.Invoke(this, BuildAssociation(useKeyFile, keyFilePath, useUserAccount));
			UnlockSucceeded?.Invoke(this, ck!);
		}

		/// <summary>Cancels an in-progress derivation operation.</summary>
		public void CancelUnlock() => _currentLogger?.Cancel();

		private async Task ExecuteBrowseKeyFileAsync()
		{
			if (_fileDialogService == null) return;

			string? path = await _fileDialogService.OpenFileAsync(
				"Select Key File", KeyFileFilters);

			if (string.IsNullOrWhiteSpace(path)) return;

			KeyFilePath = path;
			UseKeyFile = true;
			UnlockCommand.NotifyCanExecuteChanged();
		}

		// ------------------------------------------------------------------ //
		// Private helpers                                                      //
		// ------------------------------------------------------------------ //

		private static CompositeKey? BuildKey(
			byte[]? pbPasswordUtf8,
			bool useKeyFile,
			string keyFilePath,
			bool useUserAccount,
			IOConnectionInfo ioc,
			IStatusLogger logger)
		{
			var ck = new CompositeKey();

			if (pbPasswordUtf8 != null)
				ck.AddUserKey(new KcpPassword(pbPasswordUtf8));

			if (useKeyFile && !string.IsNullOrWhiteSpace(keyFilePath))
				ck.AddUserKey(new KcpKeyFile(keyFilePath));

			if (useUserAccount)
				ck.AddUserKey(new KcpUserAccount());

			if (ck.UserKeyCount == 0) return null;

			// Check if the caller has already requested cancellation before
			// proceeding to the (potentially expensive) transform step.
			if (!logger.ContinueWork())
				throw new OperationCanceledException();

			return ck;
		}

		private void ApplySavedAssociation(KeyAssociationData? assoc)
		{
			if (assoc == null) return;

			_databasePath = assoc.DatabasePath;
			_useKeyFile = !string.IsNullOrEmpty(assoc.KeyFilePath);
			_keyFilePath = assoc.KeyFilePath;
			_useUserAccount = assoc.UseUserAccount;
			// HasPassword is intentionally not applied — never pre-fill passwords.
		}

		private KeyAssociationData BuildAssociation(bool useKeyFile, string keyFilePath, bool useUserAccount)
		{
			return new KeyAssociationData
			{
				DatabasePath = _ioc.Path,
				HasPassword = !_masterPassword.IsEmpty,
				KeyFilePath = useKeyFile ? keyFilePath : string.Empty,
				UseUserAccount = useUserAccount
			};
		}
	}
}
