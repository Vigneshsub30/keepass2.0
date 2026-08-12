using System;
using System.Threading;

using KeePassLib.Interfaces;

namespace KeePass.Core.ViewModels
{
	/// <summary>
	/// <see cref="IStatusLogger"/> adapter that forwards progress updates to an
	/// <see cref="Action{Double}"/> delegate, allowing the <see cref="KeyPromptViewModel"/>
	/// to expose derivation progress without depending on WinForms controls.
	/// </summary>
	internal sealed class ViewModelProgressLogger : IStatusLogger
	{
		private readonly Action<double> _onProgress;
		private volatile bool _cancelled;

		/// <param name="onProgress">
		/// Invoked each time progress changes, with a value in [0, 100].
		/// </param>
		internal ViewModelProgressLogger(Action<double> onProgress)
		{
			_onProgress = onProgress ?? throw new ArgumentNullException(nameof(onProgress));
		}

		public void StartLogging(string strOperation, bool bWriteOperationToLog) { }
		public void EndLogging() { }

		public bool SetProgress(uint uPercent)
		{
			_onProgress(Math.Min(100.0, uPercent));
			return !_cancelled;
		}

		public bool SetText(string strNewText, LogStatusType lsType) => !_cancelled;

		public bool ContinueWork() => !_cancelled;

		/// <summary>Signals the logger that the operation should be aborted.</summary>
		internal void Cancel() => _cancelled = true;
	}
}
