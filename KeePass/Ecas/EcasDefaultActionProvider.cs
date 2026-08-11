/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using KeePass.Core.Services;
using KeePass.DataExchange;
using KeePass.Forms;
using KeePass.Native;
using KeePass.Resources;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Delegates;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;

using NativeLib = KeePassLib.Native.NativeLib;

namespace KeePass.Ecas
{
	internal sealed class EcasDefaultActionProvider : EcasActionProvider
	{
		private const uint IdWindowNormal = 0;
		private const uint IdWindowHidden = 1;
		private const uint IdWindowMin = 2;
		private const uint IdWindowMax = 3;

		private const uint IdTriggerOff = 0;
		private const uint IdTriggerOn = 1;
		private const uint IdTriggerToggle = 2;

		private const uint IdMbcY = 0;
		private const uint IdMbcN = 1;

		private const uint IdMbaNone = 0;
		private const uint IdMbaAbort = 1;
		private const uint IdMbaCmd = 2;

		// Platform-neutral replacements for MessageBoxIcon enum values.
		// These match the uint casts used in the original serialised trigger XML
		// so changing them would break existing trigger configurations.
		private const uint IconNone        = 0;   // MessageBoxIcon.None
		private const uint IconInformation = 64;  // MessageBoxIcon.Information
		private const uint IconQuestion    = 32;  // MessageBoxIcon.Question
		private const uint IconWarning     = 48;  // MessageBoxIcon.Warning
		private const uint IconError       = 16;  // MessageBoxIcon.Error

		// Platform-neutral replacements for MessageBoxButtons enum values.
		private const uint BtnsOK       = 0; // MessageBoxButtons.OK
		private const uint BtnsOKCancel = 1; // MessageBoxButtons.OKCancel
		private const uint BtnsYesNo    = 4; // MessageBoxButtons.YesNo

		// Platform-neutral UI service injected at construction time.
		private readonly IUICommandService m_uiService;

		/// <summary>
		/// Initialises the action provider using the supplied
		/// <paramref name="uiService"/>.  When <c>null</c> the legacy
		/// <see cref="Program.MainForm"/> path is used as a fallback so that
		/// existing code that calls <c>new EcasDefaultActionProvider()</c>
		/// continues to work.
		/// </summary>
		public EcasDefaultActionProvider(IUICommandService uiService = null)
		{
			m_uiService = uiService ?? new KeePass.Services.WinFormsUICommandService();
			InitActions();
		}

		private void InitActions()
		{
			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0xDA, 0xE5, 0xF8, 0x3B, 0x07, 0x30, 0x4C, 0x13,
				0x9E, 0xEF, 0x2E, 0xBA, 0xCB, 0x6E, 0xE4, 0xC7 }),
				KPRes.ExecuteCmdLineUrl, PwIcon.Console, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.Arguments, EcasValueType.String, null),
					new EcasParameter(KPRes.WaitForExit, EcasValueType.Bool, null),
					new EcasParameter(KPRes.WindowStyle, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(IdWindowNormal, KPRes.Normal),
							new EcasEnumItem(IdWindowHidden, KPRes.Hidden),
							new EcasEnumItem(IdWindowMin, KPRes.Minimized),
							new EcasEnumItem(IdWindowMax, KPRes.Maximized) })),
					new EcasParameter(KPRes.Verb, EcasValueType.String, null) },
				ExecuteShellCmd));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0xB6, 0x46, 0xA6, 0x9F, 0xDE, 0x94, 0x4B, 0xB9,
				0x9B, 0xAE, 0x3C, 0xA4, 0x7E, 0xCC, 0x10, 0xEA }),
				KPRes.TriggerStateChange, PwIcon.Run, new EcasParameter[] {
					new EcasParameter(KPRes.TriggerName, EcasValueType.String, null),
					new EcasParameter(KPRes.NewState, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(IdTriggerOn, KPRes.On),
							new EcasEnumItem(IdTriggerOff, KPRes.Off),
							new EcasEnumItem(IdTriggerToggle, KPRes.Toggle) })) },
				ChangeTriggerOnOff));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0xFD, 0x41, 0x55, 0xD5, 0x79, 0x8F, 0x44, 0xFA,
				0xAB, 0x89, 0xF2, 0xF8, 0x70, 0xEF, 0x94, 0xB8 }),
				KPRes.OpenDatabaseFileStc, PwIcon.FolderOpen, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.IOConnection + " - " + KPRes.UserNameStc,
						EcasValueType.String, null),
					new EcasParameter(KPRes.IOConnection + " - " + KPRes.Password,
						EcasValueType.String, null),
					new EcasParameter(KPRes.Password, EcasValueType.String, null),
					new EcasParameter(KPRes.KeyFile, EcasValueType.String, null),
					new EcasParameter(KPRes.WindowsUserAccount, EcasValueType.Bool, null) },
				OpenDatabaseFile));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0xF5, 0x57, 0x61, 0x4B, 0xF8, 0x4C, 0x41, 0x5D,
				0xA9, 0x13, 0x7A, 0x39, 0xCD, 0x10, 0xF0, 0xBD }),
				KPRes.SaveDatabaseStc, PwIcon.Disk, null,
				SaveDatabaseFile));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x22, 0xAD, 0x77, 0xE4, 0x17, 0x78, 0x4E, 0xED,
				0x99, 0xB4, 0x57, 0x1D, 0x02, 0xB3, 0xAD, 0x4D }),
				KPRes.SynchronizeStc, PwIcon.PaperReady, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.IOConnection + " - " + KPRes.UserNameStc,
						EcasValueType.String, null),
					new EcasParameter(KPRes.IOConnection + " - " + KPRes.Password,
						EcasValueType.String, null),
					new EcasParameter(KPRes.OnError + " - " + KPRes.Silent,
						EcasValueType.Bool, null),
					new EcasParameter(KPRes.OnError + " - " + KPRes.Continue,
						EcasValueType.Bool, null) },
				SyncDatabaseFile));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x80, 0xE6, 0x7F, 0x4E, 0x72, 0xF1, 0x40, 0x45,
				0x91, 0x76, 0x1F, 0x2C, 0x23, 0xD8, 0xEC, 0xBE }),
				KPRes.ImportStc, PwIcon.PaperReady, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.FileFormatStc, EcasValueType.String, null),
					new EcasParameter(KPRes.Method, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem((uint)PwMergeMethod.None, KPRes.Default),
							new EcasEnumItem((uint)PwMergeMethod.CreateNewUuids,
								StrUtil.RemoveAccelerator(KPRes.CreateNewIDs)),
							new EcasEnumItem((uint)PwMergeMethod.KeepExisting,
								StrUtil.RemoveAccelerator(KPRes.KeepExisting)),
							new EcasEnumItem((uint)PwMergeMethod.OverwriteExisting,
								StrUtil.RemoveAccelerator(KPRes.OverwriteExisting)),
							new EcasEnumItem((uint)PwMergeMethod.OverwriteIfNewer,
								StrUtil.RemoveAccelerator(KPRes.OverwriteIfNewer)),
							new EcasEnumItem((uint)PwMergeMethod.Synchronize,
								StrUtil.RemoveAccelerator(KPRes.OverwriteIfNewerAndApplyDel)) })),
					new EcasParameter(KPRes.Password, EcasValueType.String, null),
					new EcasParameter(KPRes.KeyFile, EcasValueType.String, null),
					new EcasParameter(KPRes.WindowsUserAccount, EcasValueType.Bool, null) },
				ImportIntoCurrentDatabase));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x0F, 0x9A, 0x6B, 0x5B, 0xCE, 0xD5, 0x46, 0xBE,
				0xB9, 0x34, 0xED, 0xB1, 0x3F, 0x94, 0x48, 0x22 }),
				KPRes.ExportStc, PwIcon.Disk, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.FileFormatStc, EcasValueType.String, null),
					new EcasParameter(KPRes.Filter + " - " + KPRes.Group, EcasValueType.String, null),
					new EcasParameter(KPRes.Filter + " - " + KPRes.Tag, EcasValueType.String, null) },
				ExportDatabaseFile));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x5B, 0xBF, 0x45, 0x9D, 0x54, 0xBF, 0x49, 0xBD,
				0x97, 0xFB, 0x2C, 0xEE, 0x5F, 0x99, 0x0A, 0x67 }),
				KPRes.CloseActiveDatabase, PwIcon.PaperReady, null,
				CloseDatabaseFile));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x3F, 0xB8, 0x33, 0x2D, 0xD6, 0x16, 0x4E, 0x87,
				0x99, 0x05, 0x64, 0xDB, 0x16, 0x4C, 0xD6, 0x26 }),
				KPRes.ActivateDatabaseTab, PwIcon.List, new EcasParameter[] {
					new EcasParameter(KPRes.FileOrUrl, EcasValueType.String, null),
					new EcasParameter(KPRes.Filter, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(0, KPRes.All),
							new EcasEnumItem(1, KPRes.Triggering) })) },
				ActivateDatabaseTab));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x3B, 0x3D, 0x3E, 0x31, 0xE4, 0xB3, 0x42, 0xA6,
				0xBA, 0xCC, 0xD5, 0xC0, 0x3B, 0xAC, 0xA9, 0x69 }),
				KPRes.Wait, PwIcon.Clock, new EcasParameter[] {
					new EcasParameter(KPRes.TimeSpan + " [ms]", EcasValueType.UInt64, null) },
				ExecuteSleep));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x09, 0xF7, 0x8F, 0x73, 0x24, 0xEC, 0x4F, 0xEC,
				0x88, 0xB6, 0x25, 0xD5, 0x30, 0xF4, 0x34, 0x6E }),
				KPRes.ShowMessageBox, PwIcon.UserCommunication, new EcasParameter[] {
					new EcasParameter(KPRes.MainInstruction, EcasValueType.String, null),
					new EcasParameter(KPRes.Text, EcasValueType.String, null),
				new EcasParameter(KPRes.Icon, EcasValueType.EnumStrings,
					new EcasEnum(new EcasEnumItem[] {
						new EcasEnumItem(IconNone,        KPRes.None),
						new EcasEnumItem(IconInformation, "i"),
						new EcasEnumItem(IconQuestion,    "?"),
						new EcasEnumItem(IconWarning,     KPRes.Warning),
						new EcasEnumItem(IconError,       KPRes.Error) })),
				new EcasParameter(KPRes.Buttons, EcasValueType.EnumStrings,
					new EcasEnum(new EcasEnumItem[] {
						new EcasEnumItem(BtnsOK,       KPRes.Ok),
						new EcasEnumItem(BtnsOKCancel, KPRes.Ok + "/" + KPRes.Cancel),
						new EcasEnumItem(BtnsYesNo,    KPRes.Yes + "/" + KPRes.No) })),
					new EcasParameter(KPRes.ButtonDefault, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(0, KPRes.Button + " 1"),
							new EcasEnumItem(1, KPRes.Button + " 2") })),
					new EcasParameter(KPRes.Action + " - " + KPRes.Condition, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(IdMbcY, KPRes.Button + " " +
								KPRes.Ok + "/" + KPRes.Yes),
							new EcasEnumItem(IdMbcN, KPRes.Button + " " +
								KPRes.Cancel + "/" + KPRes.No) })),
					new EcasParameter(KPRes.Action, EcasValueType.EnumStrings,
						new EcasEnum(new EcasEnumItem[] {
							new EcasEnumItem(IdMbaNone, KPRes.None),
							new EcasEnumItem(IdMbaAbort, KPRes.AbortTrigger),
							new EcasEnumItem(IdMbaCmd, KPRes.ExecuteCmdLineUrl) })),
					new EcasParameter(KPRes.Action + " - " + KPRes.Parameters,
						EcasValueType.String, null) },
				ShowMessageBox));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x40, 0x69, 0xA5, 0x36, 0x57, 0x1B, 0x47, 0x92,
				0xA9, 0xB3, 0x73, 0x65, 0x30, 0xE0, 0xCF, 0xC3 }),
				KPRes.PerformGlobalAutoType, PwIcon.Run, null, ExecuteGlobalAutoType));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x31, 0x70, 0x8F, 0xAD, 0x64, 0x93, 0x43, 0xF5,
				0x94, 0xEE, 0xC8, 0x1A, 0x23, 0x6E, 0x32, 0x4D }),
				KPRes.PerformSelectedAutoType, PwIcon.Run, new EcasParameter[] {
					new EcasParameter(KPRes.Sequence, EcasValueType.String, null) },
				ExecuteSelectedAutoType));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x42, 0xE8, 0x37, 0x81, 0x73, 0xD3, 0x4E, 0xEC,
				0x81, 0x48, 0x9E, 0x3B, 0x36, 0xAC, 0x83, 0x84 }),
				KPRes.ShowEntriesByTag, PwIcon.List, new EcasParameter[] {
					new EcasParameter(KPRes.Tag, EcasValueType.String, null) },
				ShowEntriesByTag));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0x95, 0x81, 0x8F, 0x45, 0x99, 0x66, 0x49, 0x88,
				0xAB, 0x3E, 0x86, 0xE8, 0x1A, 0x96, 0x68, 0x36 }),
				KPRes.CustomTbButtonAdd, PwIcon.Screen, new EcasParameter[] {
					new EcasParameter(KPRes.Id, EcasValueType.String, null),
					new EcasParameter(KPRes.Name, EcasValueType.String, null),
					new EcasParameter(KPRes.Description, EcasValueType.String, null) },
				AddToolBarButton));

			m_actions.Add(new EcasActionType(new PwUuid(new byte[] {
				0xD6, 0x6D, 0x41, 0xA2, 0x6C, 0xB2, 0x44, 0xBA,
				0xA4, 0x48, 0x0A, 0x41, 0xFA, 0x09, 0x48, 0x79 }),
				KPRes.CustomTbButtonRemove, PwIcon.Screen, new EcasParameter[] {
					new EcasParameter(KPRes.Id, EcasValueType.String, null) },
				RemoveToolBarButton));
		}

		private void ExecuteShellCmd(EcasAction a, EcasContext ctx)
		{
			string strCmd = EcasUtil.GetParamPath(a.Parameters, 0, false);
			string strArgs = EcasUtil.GetParamString(a.Parameters, 1, true, true);
			bool bWait = EcasUtil.GetParamBool(a.Parameters, 2);
			uint uWindowStyle = EcasUtil.GetParamUInt(a.Parameters, 3);
			string strVerb = EcasUtil.GetParamString(a.Parameters, 4, true);

			if(string.IsNullOrEmpty(strCmd)) return;

			Process p = null;
			try
			{
				PwEntry pe = null;
				try { pe = m_uiService.GetSelectedEntry(false); }
				catch(Exception) { Debug.Assert(false); }

				strCmd = WinUtil.CompileUrl(strCmd, pe, true, null, false);
				if(string.IsNullOrEmpty(strCmd)) return; // Might be placeholder only

				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = strCmd;
				if(!string.IsNullOrEmpty(strArgs)) psi.Arguments = strArgs;

				bool bShEx = true;
				if(!string.IsNullOrEmpty(strVerb)) { } // Need ShellExecute
				else if((uWindowStyle == IdWindowMin) ||
					(uWindowStyle == IdWindowMax)) { } // Need ShellExecute
				else
				{
					string strCmdFlt = strCmd.TrimEnd(new char[] { '\"', '\'',
						' ', '\t', '\r', '\n' });
					if(strCmdFlt.EndsWith(".exe", StrUtil.CaseIgnoreCmp) ||
						strCmdFlt.EndsWith(".com", StrUtil.CaseIgnoreCmp))
						bShEx = false;
				}
				psi.UseShellExecute = bShEx;

				if(uWindowStyle == IdWindowHidden)
				{
					psi.CreateNoWindow = true;
					psi.WindowStyle = ProcessWindowStyle.Hidden;
				}
				else if(uWindowStyle == IdWindowMin)
					psi.WindowStyle = ProcessWindowStyle.Minimized;
				else if(uWindowStyle == IdWindowMax)
					psi.WindowStyle = ProcessWindowStyle.Maximized;

				if(!string.IsNullOrEmpty(strVerb))
					psi.Verb = strVerb;

				p = NativeLib.StartProcessEx(psi);

				if((p != null) && bWait)
				{
					m_uiService.SetInteractionBlocked(true);
					MessageService.ExternalIncrementMessageCount();

					try { p.WaitForExit(); }
					catch(Exception) { Debug.Assert(false); }

					MessageService.ExternalDecrementMessageCount();
					m_uiService.SetInteractionBlocked(false);
				}
			}
			catch(Exception ex) { throw new ExtendedException(strCmd, ex); }
			finally
			{
				try { if(p != null) p.Dispose(); }
				catch(Exception) { Debug.Assert(false); }
			}
		}

		private void ChangeTriggerOnOff(EcasAction a, EcasContext ctx)
		{
			string strName = EcasUtil.GetParamString(a.Parameters, 0, true);
			uint uState = EcasUtil.GetParamUInt(a.Parameters, 1);

			EcasTrigger t = null;
			if(strName.Length == 0) t = ctx.Trigger;
			else
			{
				foreach(EcasTrigger trg in ctx.TriggerSystem.TriggerCollection)
				{
					if(trg.Name == strName) { t = trg; break; }
				}
			}

			if(t == null) throw new Exception(KPRes.ObjectNotFound +
				MessageService.NewParagraph + KPRes.TriggerName + ": " + strName + ".");

			if(uState == IdTriggerOn) t.On = true;
			else if(uState == IdTriggerOff) t.On = false;
			else if(uState == IdTriggerToggle) t.On = !t.On;
			else { Debug.Assert(false); }
		}

		private void OpenDatabaseFile(EcasAction a, EcasContext ctx)
		{
			string strPath = EcasUtil.GetParamPath(a.Parameters, 0, true);
			if(string.IsNullOrEmpty(strPath)) return;

			string strIOUserName = EcasUtil.GetParamString(a.Parameters, 1, true);
			string strIOPassword = EcasUtil.GetParamString(a.Parameters, 2, true);

			IOConnectionInfo ioc = IOFromParameters(strPath, strIOUserName, strIOPassword);
			if(ioc == null) return;

			CompositeKey ck = KeyFromParams(a, 3, 4, 5, ioc);

			m_uiService.OpenDatabase(ioc, ck, ioc.IsLocalFile());
		}

		private void SaveDatabaseFile(EcasAction a, EcasContext ctx)
		{
			m_uiService.SaveActiveDatabase();
		}

		private static CompositeKey KeyFromParams(EcasAction a, int iPassword,
			int iKeyFile, int iUserAccount, IOConnectionInfo ioc)
		{
			string strPassword = EcasUtil.GetParamString(a.Parameters, iPassword, true);
			string strKeyFile = EcasUtil.GetParamPath(a.Parameters, iKeyFile, true);
			bool bUserAccount = EcasUtil.GetParamBool(a.Parameters, iUserAccount);

			byte[] pbPasswordUtf8 = null;
			if(!string.IsNullOrEmpty(strPassword))
				pbPasswordUtf8 = StrUtil.Utf8.GetBytes(strPassword);

			return KeyUtil.CreateKey(pbPasswordUtf8, strKeyFile, bUserAccount,
				ioc, false, false);
		}

		private void SyncDatabaseFile(EcasAction a, EcasContext ctx)
		{
			string[] vPaths = EcasUtil.GetParamPaths(a.Parameters, 0, true);
			if((vPaths == null) || (vPaths.Length == 0)) return;

			string strIOUserName = EcasUtil.GetParamString(a.Parameters, 1, true);
			string strIOPassword = EcasUtil.GetParamString(a.Parameters, 2, true);
			bool bOnErrorSilent = EcasUtil.GetParamBool(a.Parameters, 3);
			bool bOnErrorContinue = EcasUtil.GetParamBool(a.Parameters, 4);

			PwDatabase pd = m_uiService.GetActiveDatabase();
			if((pd == null) || !pd.IsOpen) return;

			List<IOConnectionInfo> l = new List<IOConnectionInfo>();
			foreach(string strPath in vPaths)
			{
				IOConnectionInfo ioc = IOFromParameters(strPath, strIOUserName, strIOPassword);
				if(ioc != null) l.Add(ioc);
			}
			if(l.Count == 0) return;

			MainForm mf = Program.MainForm;
			bool? ob = ImportUtil.Synchronize(pd, mf, l.ToArray(), false, mf,
				bOnErrorSilent, bOnErrorContinue);
			if(mf != null) mf.UpdateUISyncPost(ob);
		}

		private IOConnectionInfo IOFromParameters(string strPath,
			string strUser, string strPassword)
		{
			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strPath);

			// Set the user name, which acts as a filter for the MRU items
			if(!string.IsNullOrEmpty(strUser)) ioc.UserName = strUser;

			// Try to complete it using the MRU list; this will especially
			// retrieve the CredSaveMode of the MRU item (if one exists)
			ioc = m_uiService.CompleteConnectionInfoUsingMru(ioc);

			// Override the password using the trigger value; do not change
			// the CredSaveMode anymore (otherwise e.g. values retrieved
			// using field references would be stored in the MRU list)
			if(!string.IsNullOrEmpty(strPassword)) ioc.Password = strPassword;

			if(ioc.Password.Length > 0) ioc.IsComplete = true;

			return MainForm.CompleteConnectionInfo(ioc, false, true, true, false);
		}

		private void ImportIntoCurrentDatabase(EcasAction a, EcasContext ctx)
		{
			PwDatabase pd = m_uiService.GetActiveDatabase();
			if((pd == null) || !pd.IsOpen) return;

			string strPath = EcasUtil.GetParamPath(a.Parameters, 0, true);
			if(string.IsNullOrEmpty(strPath)) return;
			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strPath);

			string strFormat = EcasUtil.GetParamString(a.Parameters, 1, true);
			if(string.IsNullOrEmpty(strFormat)) return;
			FileFormatProvider ff = Program.FileFormatPool.Find(strFormat);
			if(ff == null)
				throw new Exception(KPRes.Unknown + ": " + strFormat);

			uint uMethod = EcasUtil.GetParamUInt(a.Parameters, 2);
			Type tMM = Enum.GetUnderlyingType(typeof(PwMergeMethod));
			object oMethod = Convert.ChangeType(uMethod, tMM);
			PwMergeMethod mm = PwMergeMethod.None;
			if(Enum.IsDefined(typeof(PwMergeMethod), oMethod))
				mm = (PwMergeMethod)oMethod;
			else { Debug.Assert(false); }
			if(mm == PwMergeMethod.None) mm = PwMergeMethod.CreateNewUuids;

			CompositeKey ck = KeyFromParams(a, 3, 4, 5, ioc);
			if((ck == null) && ff.RequiresKey)
			{
				KeyPromptFormResult r;
				DialogResult dr = KeyPromptForm.ShowDialog(ioc, false, null, out r);
				if((dr != DialogResult.OK) || (r == null)) return;

				ck = r.CompositeKey;
			}

			bool? ob = false; // Exception => UI update
			try { ob = ImportUtil.Import(pd, ff, ioc, mm, ck); }
			finally
			{
				if(ob.HasValue)
			{
				MainForm mf = Program.MainForm;
				if(mf != null) mf.UpdateUI(false, null, true, null, true, null, false);
			}
			}
		}

		private void ExportDatabaseFile(EcasAction a, EcasContext ctx)
		{
			string strPath = EcasUtil.GetParamPath(a.Parameters, 0, true);
			// if(string.IsNullOrEmpty(strPath)) return; // Allow no-file exports
			string strFormat = EcasUtil.GetParamString(a.Parameters, 1, true);
			if(string.IsNullOrEmpty(strFormat)) return;
			string strGroup = EcasUtil.GetParamString(a.Parameters, 2, true);
			string strTag = EcasUtil.GetParamString(a.Parameters, 3, true);

			PwDatabase pd = m_uiService.GetActiveDatabase();
			if((pd == null) || !pd.IsOpen) return;

			PwGroup pg = pd.RootGroup;
			if(!string.IsNullOrEmpty(strGroup))
			{
				char chSep = strGroup[0];
				PwGroup pgSub = pg.FindCreateSubTree(strGroup.Substring(1),
					new char[] { chSep }, false);
				pg = (pgSub ?? (new PwGroup(true, true, KPRes.Group, PwIcon.Folder)));
			}

			strTag = StrUtil.NormalizeTag(strTag);
			if(!string.IsNullOrEmpty(strTag))
			{
				pg = pg.CloneDeep();

				GroupHandler gh = delegate(PwGroup pgSub)
				{
					PwObjectList<PwEntry> l = pgSub.Entries;
					long n = (long)l.UCount;
					for(long i = n - 1; i >= 0; --i)
					{
						List<string> lTags = l.GetAt((uint)i).GetTagsInherited();
						if(!lTags.Contains(strTag))
							l.RemoveAt((uint)i);
					}

					return true;
				};

				gh(pg);
				pg.TraverseTree(TraversalMethod.PreOrder, gh, null);
			}

			PwExportInfo pei = new PwExportInfo(pg, pd, true);
			IOConnectionInfo ioc = (!string.IsNullOrEmpty(strPath) ?
				IOConnectionInfo.FromPath(strPath) : null);
			ExportUtil.Export(pei, strFormat, ioc);
		}

		private void CloseDatabaseFile(EcasAction a, EcasContext ctx)
		{
			m_uiService.CloseActiveDatabase(true);
		}

		private void ActivateDatabaseTab(EcasAction a, EcasContext ctx)
		{
			string strName = EcasUtil.GetParamPath(a.Parameters, 0, true);
			bool bEmptyName = string.IsNullOrEmpty(strName);

			uint uSel = EcasUtil.GetParamUInt(a.Parameters, 1, 0);
			PwDatabase pdSel = ctx.Properties.Get<PwDatabase>(EcasProperty.Database);

			DocumentManagerEx dm = m_uiService.GetDocumentManager() as DocumentManagerEx;
			if(dm == null) return;
			foreach(PwDocument doc in dm.Documents)
			{
				if(doc.Database == null) { Debug.Assert(false); continue; }

				if(uSel == 0) // Select from all
				{
					if(bEmptyName) continue; // Name required in this case
				}
				else if(uSel == 1) // Triggering only
				{
					if(!object.ReferenceEquals(doc.Database, pdSel)) continue;
				}
				else { Debug.Assert(false); continue; }

				IOConnectionInfo ioc = null;
				if((doc.LockedIoc != null) && !string.IsNullOrEmpty(doc.LockedIoc.Path))
					ioc = doc.LockedIoc;
				else if((doc.Database.IOConnectionInfo != null) &&
					!string.IsNullOrEmpty(doc.Database.IOConnectionInfo.Path))
					ioc = doc.Database.IOConnectionInfo;

				if(bEmptyName || ((ioc != null) && (ioc.Path.IndexOf(strName,
					StrUtil.CaseIgnoreCmp) >= 0)))
				{
					m_uiService.MakeDocumentActive(doc);
					break;
				}
			}
		}

		private static void ExecuteSleep(EcasAction a, EcasContext ctx)
		{
			uint uTimeSpan = EcasUtil.GetParamUInt(a.Parameters, 0);

			if((uTimeSpan != 0) && (uTimeSpan <= (uint)int.MaxValue))
				Thread.Sleep((int)uTimeSpan);
		}

		private void ExecuteGlobalAutoType(EcasAction a, EcasContext ctx)
		{
			m_uiService.ExecuteGlobalAutoType();
		}

		private void ExecuteSelectedAutoType(EcasAction a, EcasContext ctx)
		{
			try
			{
				// Do not Spr-compile the sequence here; it'll be compiled by
				// the auto-type engine (and this expects an auto-type sequence
				// as input, not a data string; compiling it here would e.g.
				// result in broken '%' characters in passwords)
				string strSeq = EcasUtil.GetParamString(a.Parameters, 0, false);
				if(string.IsNullOrEmpty(strSeq)) strSeq = null;

				PwEntry pe = m_uiService.GetSelectedEntry(true);
				if(pe == null) return;
				DocumentManagerEx dm = m_uiService.GetDocumentManager() as DocumentManagerEx;
				PwDatabase pd = (dm != null) ? dm.SafeFindContainerOf(pe) : null;

				MainForm mf = Program.MainForm;
				IntPtr hFg = NativeMethods.GetForegroundWindowHandle();
				if(GlobalWindowManager.HasWindowEx(hFg))
					AutoType.PerformIntoPreviousWindow(mf, pe, pd, strSeq);
				else AutoType.PerformIntoCurrentWindow(pe, pd, strSeq);
			}
			catch(Exception) { Debug.Assert(false); }
		}

		private void ShowEntriesByTag(EcasAction a, EcasContext ctx)
		{
			string strTag = EcasUtil.GetParamString(a.Parameters, 0, true);
			m_uiService.ShowEntriesByTag(strTag);
		}

		private void AddToolBarButton(EcasAction a, EcasContext ctx)
		{
			string strID = EcasUtil.GetParamString(a.Parameters, 0, true);
			string strName = EcasUtil.GetParamString(a.Parameters, 1, true);
			string strDesc = EcasUtil.GetParamString(a.Parameters, 2, true);

			m_uiService.AddCustomToolBarButton(strID, strName, strDesc);
		}

		private void RemoveToolBarButton(EcasAction a, EcasContext ctx)
		{
			string strID = EcasUtil.GetParamString(a.Parameters, 0, true);
			m_uiService.RemoveCustomToolBarButton(strID);
		}

		private void ShowMessageBox(EcasAction a, EcasContext ctx)
		{
			VistaTaskDialog vtd = new VistaTaskDialog();

			string strMain = EcasUtil.GetParamString(a.Parameters, 0, true);
			if(!string.IsNullOrEmpty(strMain)) vtd.MainInstruction = strMain;

			string strText = EcasUtil.GetParamString(a.Parameters, 1, true);
			if(!string.IsNullOrEmpty(strText)) vtd.Content = strText;

			uint uIcon = EcasUtil.GetParamUInt(a.Parameters, 2, 0);
			if(uIcon == IconInformation)
				vtd.SetIcon(VtdIcon.Information);
			else if(uIcon == IconQuestion)
				vtd.SetIcon(VtdCustomIcon.Question);
			else if(uIcon == IconWarning)
				vtd.SetIcon(VtdIcon.Warning);
			else if(uIcon == IconError)
				vtd.SetIcon(VtdIcon.Error);
			else { Debug.Assert(uIcon == IconNone); }

			vtd.CommandLinks = false;

			// Button IDs use DialogResult int values for round-trip stability
			// with existing serialised trigger XML.
			const int DrOk     = (int)DialogResult.OK;     // 1
			const int DrCancel = (int)DialogResult.Cancel; // 2

			uint uBtns = EcasUtil.GetParamUInt(a.Parameters, 3, 0);
			bool bCanCancel = false;
			if(uBtns == BtnsOKCancel)
			{
				vtd.AddButton(DrOk,     KPRes.Ok,     null);
				vtd.AddButton(DrCancel, KPRes.Cancel, null);
				bCanCancel = true;
			}
			else if(uBtns == BtnsYesNo)
			{
				vtd.AddButton(DrOk,     KPRes.YesCmd, null);
				vtd.AddButton(DrCancel, KPRes.NoCmd,  null);
				bCanCancel = true;
			}
			else vtd.AddButton(DrOk, KPRes.Ok, null);

			uint uDef = EcasUtil.GetParamUInt(a.Parameters, 4, 0);
			ReadOnlyCollection<VtdButton> lButtons = vtd.Buttons;
			if(uDef < (uint)lButtons.Count)
				vtd.DefaultButtonID = lButtons[(int)uDef].ID;

			vtd.WindowTitle = PwDefs.ShortProductName;

			string strTrg = ctx.Trigger.Name;
			if(!string.IsNullOrEmpty(strTrg))
			{
				vtd.FooterText = KPRes.Trigger + @": '" + strTrg + @"'.";
				vtd.SetFooterIcon(VtdIcon.Information);
			}

			int dr;
			if(vtd.ShowDialog()) dr = vtd.Result;
			else
			{
				string str = (strMain ?? string.Empty);
				if(!string.IsNullOrEmpty(strText))
				{
					if(str.Length > 0) str += MessageService.NewParagraph;
					str += strText;
				}

				MessageBoxDefaultButton mbdb = MessageBoxDefaultButton.Button1;
				if(uDef == 1) mbdb = MessageBoxDefaultButton.Button2;
				else if(uDef == 2) mbdb = MessageBoxDefaultButton.Button3;

				MessageService.ExternalIncrementMessageCount();
				try
				{
					dr = (int)MessageService.SafeShowMessageBox(str,
						PwDefs.ShortProductName, (MessageBoxButtons)uBtns,
						(MessageBoxIcon)uIcon, mbdb);
				}
				finally { MessageService.ExternalDecrementMessageCount(); }
			}

			uint uActCondID = EcasUtil.GetParamUInt(a.Parameters, 5, 0);

			bool bDrY = ((dr == DrOk) || (dr == (int)DialogResult.Yes));
			bool bDrN = ((dr == DrCancel) || (dr == (int)DialogResult.No));

			bool bPerformAction = (((uActCondID == IdMbcY) && bDrY) ||
				((uActCondID == IdMbcN) && bDrN));
			if(!bPerformAction) return;

			uint uActID = EcasUtil.GetParamUInt(a.Parameters, 6, 0);
			string strActionParam = EcasUtil.GetParamString(a.Parameters, 7, true);

			if(uActID == IdMbaNone) { }
			else if(uActID == IdMbaAbort)
			{
				if(bCanCancel) ctx.Cancel = true;
			}
			else if(uActID == IdMbaCmd)
			{
				if(!string.IsNullOrEmpty(strActionParam))
					WinUtil.OpenUrl(strActionParam, null);
			}
			else { Debug.Assert(false); }
		}
	}
}
