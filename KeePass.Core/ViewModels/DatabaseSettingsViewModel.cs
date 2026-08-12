#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Utility;

namespace KeePass.Core.ViewModels
{
	// ======================================================================
	// Cipher item wrapper
	// ======================================================================

	/// <summary>
	/// Display wrapper for a single <see cref="ICipherEngine"/> entry.
	/// </summary>
	public sealed class CipherItemViewModel
	{
		public ICipherEngine Engine { get; }
		public string Name => Engine.DisplayName;

		public CipherItemViewModel(ICipherEngine engine)
			=> Engine = engine ?? throw new ArgumentNullException(nameof(engine));
	}

	// ======================================================================
	// KDF item wrapper
	// ======================================================================

	/// <summary>
	/// Display wrapper for a single <see cref="KdfEngine"/> entry.
	/// </summary>
	public sealed class KdfItemViewModel
	{
		public KdfEngine Engine { get; }
		public string Name => Engine.Name;

		public KdfItemViewModel(KdfEngine engine)
			=> Engine = engine ?? throw new ArgumentNullException(nameof(engine));
	}

	// ======================================================================
	// Argon2 parameters view-model
	// ======================================================================

	/// <summary>
	/// Parameters for Argon2d / Argon2id KDF.
	/// Defaults are sourced from <see cref="KdfEngine.GetDefaultParameters"/>;
	/// the internal <c>DefaultXxx</c> constants are not accessible from outside
	/// the library assembly.
	/// </summary>
	public sealed class Argon2ParametersViewModel : ObservableObject
	{
		// Argon2 defaults: 64 MB, 2 iterations, 2 parallel threads.
		private const ulong DefaultMemoryBytes    = 64UL * 1024 * 1024;
		private const ulong DefaultIterationsVal  = 2;
		private const uint  DefaultParallelismVal = 2;

		// Memory in megabytes (internally stored as bytes)
		private uint _memoryMb = (uint)(DefaultMemoryBytes / (1024 * 1024));
		public uint MemoryMb
		{
			get => _memoryMb;
			set => SetProperty(ref _memoryMb, Math.Max(8u, value));
		}

		private ulong _iterations = DefaultIterationsVal;
		public ulong Iterations
		{
			get => _iterations;
			set => SetProperty(ref _iterations, Math.Max(1u, value));
		}

		private uint _parallelism = DefaultParallelismVal;
		public uint Parallelism
		{
			get => _parallelism;
			set => SetProperty(ref _parallelism, Math.Max(1u, value));
		}

		/// <summary>
		/// Apply current values into <paramref name="kdfParameters"/>.
		/// </summary>
		public void ApplyTo(KdfParameters kdfParameters)
		{
			if (kdfParameters == null) throw new ArgumentNullException(nameof(kdfParameters));
			kdfParameters.SetUInt64(Argon2Kdf.ParamMemory, (ulong)_memoryMb * 1024 * 1024);
			kdfParameters.SetUInt64(Argon2Kdf.ParamIterations, _iterations);
			kdfParameters.SetUInt32(Argon2Kdf.ParamParallelism, _parallelism);
		}

		/// <summary>
		/// Load values from existing <paramref name="kdfParameters"/>.
		/// </summary>
		public void LoadFrom(KdfParameters kdfParameters)
		{
			if (kdfParameters == null) throw new ArgumentNullException(nameof(kdfParameters));
			ulong memBytes = kdfParameters.GetUInt64(Argon2Kdf.ParamMemory, DefaultMemoryBytes);
			MemoryMb = (uint)Math.Max(1, (long)memBytes / (1024 * 1024));
			Iterations = Math.Max(1, kdfParameters.GetUInt64(Argon2Kdf.ParamIterations, DefaultIterationsVal));
			Parallelism = Math.Max(1u, kdfParameters.GetUInt32(Argon2Kdf.ParamParallelism, DefaultParallelismVal));
		}
	}

	// ======================================================================
	// AES-KDF parameters view-model
	// ======================================================================

	/// <summary>
	/// Parameters for the legacy AES-KDF.
	/// </summary>
	public sealed class AesKdfParametersViewModel : ObservableObject
	{
		private ulong _rounds = PwDefs.DefaultKeyEncryptionRounds;
		public ulong Rounds
		{
			get => _rounds;
			set => SetProperty(ref _rounds, Math.Max(1u, value));
		}

		public void ApplyTo(KdfParameters kdfParameters)
		{
			if (kdfParameters == null) throw new ArgumentNullException(nameof(kdfParameters));
			kdfParameters.SetUInt64(AesKdf.ParamRounds, _rounds);
		}

		public void LoadFrom(KdfParameters kdfParameters)
		{
			if (kdfParameters == null) throw new ArgumentNullException(nameof(kdfParameters));
			Rounds = Math.Max(1, kdfParameters.GetUInt64(AesKdf.ParamRounds, PwDefs.DefaultKeyEncryptionRounds));
		}
	}

	// ======================================================================
	// Main database-settings view-model
	// ======================================================================

	/// <summary>
	/// ViewModel for the database settings dialog (equivalent of
	/// <c>DatabaseSettingsForm</c> in the WinForms shell). Settings are loaded
	/// from a <see cref="PwDatabase"/> on construction; calling
	/// <see cref="ApplyCommand"/> writes them back.
	/// </summary>
	public sealed class DatabaseSettingsViewModel : ObservableObject
	{
		private readonly PwDatabase _db;

		// ------------------------------------------------------------------ //
		// General tab                                                         //
		// ------------------------------------------------------------------ //

		private string _databaseName = string.Empty;
		public string DatabaseName
		{
			get => _databaseName;
			set => SetProperty(ref _databaseName, value ?? string.Empty);
		}

		private string _description = string.Empty;
		public string Description
		{
			get => _description;
			set => SetProperty(ref _description, value ?? string.Empty);
		}

		private string _defaultUserName = string.Empty;
		public string DefaultUserName
		{
			get => _defaultUserName;
			set => SetProperty(ref _defaultUserName, value ?? string.Empty);
		}

		// ------------------------------------------------------------------ //
		// Security tab – cipher                                               //
		// ------------------------------------------------------------------ //

		public ObservableCollection<CipherItemViewModel> Ciphers { get; } =
			new ObservableCollection<CipherItemViewModel>();

		private CipherItemViewModel? _selectedCipher;
		public CipherItemViewModel? SelectedCipher
		{
			get => _selectedCipher;
			set => SetProperty(ref _selectedCipher, value);
		}

		// ------------------------------------------------------------------ //
		// Security tab – KDF                                                  //
		// ------------------------------------------------------------------ //

		public ObservableCollection<KdfItemViewModel> KdfEngines { get; } =
			new ObservableCollection<KdfItemViewModel>();

		private KdfItemViewModel? _selectedKdf;
		public KdfItemViewModel? SelectedKdf
		{
			get => _selectedKdf;
			set
			{
				if (SetProperty(ref _selectedKdf, value))
					SwitchKdfParameters(value?.Engine);
			}
		}

		private ObservableObject? _kdfParameters;
		/// <summary>
		/// Currently active KDF parameter sub-VM — either
		/// <see cref="Argon2ParametersViewModel"/> or
		/// <see cref="AesKdfParametersViewModel"/>.
		/// </summary>
		public ObservableObject? KdfParameters
		{
			get => _kdfParameters;
			private set => SetProperty(ref _kdfParameters, value);
		}

		/// <summary>Typed access (nullable) for Argon2 parameter panel.</summary>
		public Argon2ParametersViewModel Argon2Params { get; } = new Argon2ParametersViewModel();

		/// <summary>Typed access (nullable) for AES-KDF parameter panel.</summary>
		public AesKdfParametersViewModel AesKdfParams { get; } = new AesKdfParametersViewModel();

		// ------------------------------------------------------------------ //
		// Security tab – benchmark                                            //
		// ------------------------------------------------------------------ //

		private string _benchmarkStatus = string.Empty;
		public string BenchmarkStatus
		{
			get => _benchmarkStatus;
			private set => SetProperty(ref _benchmarkStatus, value ?? string.Empty);
		}

		private bool _isBenchmarking;
		public bool IsBenchmarking
		{
			get => _isBenchmarking;
			private set => SetProperty(ref _isBenchmarking, value);
		}

		public IAsyncRelayCommand BenchmarkCommand { get; }

		// ------------------------------------------------------------------ //
		// Compression tab                                                     //
		// ------------------------------------------------------------------ //

		private PwCompressionAlgorithm _compression;
		public PwCompressionAlgorithm Compression
		{
			get => _compression;
			set
			{
				if (SetProperty(ref _compression, value))
				{
					OnPropertyChanged(nameof(IsCompressionNone));
					OnPropertyChanged(nameof(IsCompressionGZip));
				}
			}
		}

		public bool IsCompressionNone
		{
			get => _compression == PwCompressionAlgorithm.None;
			set { if (value) Compression = PwCompressionAlgorithm.None; }
		}

		public bool IsCompressionGZip
		{
			get => _compression == PwCompressionAlgorithm.GZip;
			set { if (value) Compression = PwCompressionAlgorithm.GZip; }
		}

		// ------------------------------------------------------------------ //
		// Recycle Bin tab                                                     //
		// ------------------------------------------------------------------ //

		private bool _recycleBinEnabled;
		public bool RecycleBinEnabled
		{
			get => _recycleBinEnabled;
			set => SetProperty(ref _recycleBinEnabled, value);
		}

		// ------------------------------------------------------------------ //
		// History tab                                                         //
		// ------------------------------------------------------------------ //

		private int _historyMaxItems;
		public int HistoryMaxItems
		{
			get => _historyMaxItems;
			set => SetProperty(ref _historyMaxItems, Math.Max(-1, value));
		}

		private long _historyMaxSize;
		public long HistoryMaxSize
		{
			get => _historyMaxSize;
			set
			{
				if (SetProperty(ref _historyMaxSize, Math.Max(-1L, value)))
					OnPropertyChanged(nameof(HistoryMaxSizeDecimal));
			}
		}

		/// <summary>
		/// Decimal bridge for Avalonia <c>NumericUpDown</c> which requires
		/// a <c>decimal?</c> value. Clamps to the representable decimal range.
		/// </summary>
		public decimal HistoryMaxSizeDecimal
		{
			get => _historyMaxSize;
			set => HistoryMaxSize = (long)Math.Clamp(value, decimal.MinValue, decimal.MaxValue);
		}

		// ------------------------------------------------------------------ //
		// Commands                                                            //
		// ------------------------------------------------------------------ //

		public IRelayCommand ApplyCommand { get; }
		public IRelayCommand CancelCommand { get; }

		/// <summary>Raised when OK is successfully applied.</summary>
		public event EventHandler? Applied;

		/// <summary>Raised when the user cancels.</summary>
		public event EventHandler? Cancelled;

		// ------------------------------------------------------------------ //
		// Constructor                                                         //
		// ------------------------------------------------------------------ //

		public DatabaseSettingsViewModel(PwDatabase db)
		{
			_db = db ?? throw new ArgumentNullException(nameof(db));

			BenchmarkCommand = new AsyncRelayCommand(ExecuteBenchmarkAsync);
			ApplyCommand     = new RelayCommand(ExecuteApply);
			CancelCommand    = new RelayCommand(ExecuteCancel);

			LoadCiphers();
			LoadKdfEngines();
			LoadFromDatabase();
		}

		// ------------------------------------------------------------------ //
		// Initialisation helpers                                              //
		// ------------------------------------------------------------------ //

		private void LoadCiphers()
		{
			CipherPool pool = CipherPool.GlobalPool;
			for (int i = 0; i < pool.EngineCount; i++)
				Ciphers.Add(new CipherItemViewModel(pool[i]));
		}

		private void LoadKdfEngines()
		{
			foreach (KdfEngine engine in KdfPool.Engines)
				KdfEngines.Add(new KdfItemViewModel(engine));
		}

		/// <summary>
		/// Populates all properties from <see cref="_db"/> settings.
		/// </summary>
		public void LoadFromDatabase()
		{
			DatabaseName   = _db.Name;
			Description    = _db.Description;
			DefaultUserName = _db.DefaultUserName;
			Compression    = _db.Compression;
			RecycleBinEnabled = _db.RecycleBinEnabled;
			HistoryMaxItems = _db.HistoryMaxItems;
			HistoryMaxSize  = _db.HistoryMaxSize;

			// Select matching cipher
			PwUuid cipherUuid = _db.DataCipherUuid;
			SelectedCipher = FindCipher(cipherUuid) ?? Ciphers[0];

			// Select matching KDF and load its parameters
			KdfParameters? dbKdf = _db.KdfParameters;
			PwUuid? kdfUuid = dbKdf?.KdfUuid;
			_selectedKdf = kdfUuid != null ? FindKdf(kdfUuid) : null;
			_selectedKdf ??= KdfEngines[0];

			SwitchKdfParameters(_selectedKdf.Engine);
			LoadKdfParametersFromDatabase(dbKdf);
		}

		private CipherItemViewModel? FindCipher(PwUuid uuid)
		{
			foreach (var c in Ciphers)
			{
				if (c.Engine.CipherUuid.Equals(uuid)) return c;
			}
			return null;
		}

		private KdfItemViewModel? FindKdf(PwUuid uuid)
		{
			foreach (var k in KdfEngines)
			{
				if (k.Engine.Uuid.Equals(uuid)) return k;
			}
			return null;
		}

		private void SwitchKdfParameters(KdfEngine? engine)
		{
			if (engine is Argon2Kdf)
				KdfParameters = Argon2Params;
			else
				KdfParameters = AesKdfParams;
		}

		private void LoadKdfParametersFromDatabase(KdfParameters? src)
		{
			if (src == null) return;

			if (_selectedKdf?.Engine is Argon2Kdf)
				Argon2Params.LoadFrom(src);
			else
				AesKdfParams.LoadFrom(src);
		}

		// ------------------------------------------------------------------ //
		// Benchmark                                                           //
		// ------------------------------------------------------------------ //

		private async Task ExecuteBenchmarkAsync(CancellationToken ct)
		{
			KdfEngine? engine = _selectedKdf?.Engine;
			if (engine == null) return;

			IsBenchmarking = true;
			BenchmarkStatus = "Benchmarking…";

			try
			{
				var progress = new Progress<string>(msg =>
				{
					BenchmarkStatus = msg;
				});

				KdfParameters best = await Task.Run(() =>
				{
					ct.ThrowIfCancellationRequested();
					return engine.GetBestParameters(1000); // 1000 ms target
				}, ct);

				// Apply benchmark result to the appropriate parameter VM.
				if (engine is Argon2Kdf)
					Argon2Params.LoadFrom(best);
				else
					AesKdfParams.LoadFrom(best);

				BenchmarkStatus = "Benchmark complete — parameters updated for ~1 s delay.";
			}
			catch (OperationCanceledException)
			{
				BenchmarkStatus = "Benchmark cancelled.";
			}
			catch (Exception ex)
			{
				BenchmarkStatus = $"Benchmark failed: {ex.Message}";
			}
			finally
			{
				IsBenchmarking = false;
			}
		}

		// ------------------------------------------------------------------ //
		// Apply / Cancel                                                      //
		// ------------------------------------------------------------------ //

		private void ExecuteApply()
		{
			_db.Name           = _databaseName;
			_db.Description    = _description;
			_db.DefaultUserName = _defaultUserName;
			_db.Compression    = _compression;
			_db.RecycleBinEnabled = _recycleBinEnabled;
			_db.HistoryMaxItems = _historyMaxItems;
			_db.HistoryMaxSize  = _historyMaxSize;

			if (_selectedCipher != null)
				_db.DataCipherUuid = _selectedCipher.Engine.CipherUuid;

			if (_selectedKdf != null)
			{
				KdfParameters newKdf = _selectedKdf.Engine.GetDefaultParameters();
				if (_selectedKdf.Engine is Argon2Kdf)
					Argon2Params.ApplyTo(newKdf);
				else
					AesKdfParams.ApplyTo(newKdf);
				_db.KdfParameters = newKdf;
			}

			Applied?.Invoke(this, EventArgs.Empty);
		}

		private void ExecuteCancel() =>
			Cancelled?.Invoke(this, EventArgs.Empty);
	}
}
