/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

using System.Collections.Generic;

using KeePass.Core.Services;
using KeePass.Ecas;

using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

using Xunit;

namespace KeePass.Tests.Ecas
{
	/// <summary>
	/// Regression tests for WO-037: verifies that the default action set
	/// registered by <c>EcasPool</c> includes the expected action UUIDs,
	/// and that <see cref="IUICommandService"/> has the expected contract.
	/// </summary>
	public sealed class EcasActionProviderTests
	{
		// ── Stub IUICommandService ─────────────────────────────────────────────

		private sealed class RecordingUiService : IUICommandService
		{
			public readonly List<string> Calls = new List<string>();

			public void OpenDatabase(IOConnectionInfo ioc, CompositeKey key, bool local)
				=> Calls.Add("OpenDatabase");

			public void SaveActiveDatabase()
				=> Calls.Add("SaveActiveDatabase");

			public void CloseActiveDatabase(bool ecas)
				=> Calls.Add("CloseActiveDatabase:" + ecas);

			public PwDatabase GetActiveDatabase()
			{
				Calls.Add("GetActiveDatabase");
				return null;
			}

			public object GetDocumentManager()
			{
				Calls.Add("GetDocumentManager");
				return null;
			}

			public void MakeDocumentActive(object doc)
				=> Calls.Add("MakeDocumentActive");

			public PwEntry GetSelectedEntry(bool withContext)
			{
				Calls.Add("GetSelectedEntry:" + withContext);
				return null;
			}

			public void ShowEntriesByTag(string tag)
				=> Calls.Add("ShowEntriesByTag:" + tag);

			public void AddCustomToolBarButton(string id, string name, string desc)
				=> Calls.Add("AddCustomToolBarButton:" + id);

			public void RemoveCustomToolBarButton(string id)
				=> Calls.Add("RemoveCustomToolBarButton:" + id);

			public void SetInteractionBlocked(bool blocked)
				=> Calls.Add("SetInteractionBlocked:" + blocked);

			public IOConnectionInfo CompleteConnectionInfoUsingMru(IOConnectionInfo ioc)
			{
				Calls.Add("CompleteConnectionInfoUsingMru");
				return ioc;
			}

			public void ExecuteGlobalAutoType()
				=> Calls.Add("ExecuteGlobalAutoType");
		}

		// ── EcasPool / action registration ────────────────────────────────────

		[Fact]
		public void EcasPool_WithDefaultProviders_FindsSaveDatabaseAction()
		{
			// EcasPool with default providers uses EcasDefaultActionProvider
			// which now routes through IUICommandService.
			var pool = new EcasPool(bAddDefaultProviders: true);

			var saveUuid = new PwUuid(new byte[] {
				0x5A, 0x71, 0xB0, 0xAB, 0xB6, 0x78, 0x49, 0x6A,
				0x83, 0xCB, 0xBB, 0x42, 0x2B, 0xF2, 0x5B, 0xDB });

			EcasActionType found = pool.FindAction(saveUuid);
			Assert.NotNull(found);
		}

		[Fact]
		public void EcasPool_WithDefaultProviders_FindsCloseDatabaseAction()
		{
			var pool = new EcasPool(bAddDefaultProviders: true);

			var closeUuid = new PwUuid(new byte[] {
				0x9B, 0x9B, 0x7A, 0x16, 0xB9, 0x9A, 0x41, 0xCE,
				0xA2, 0x71, 0xC7, 0xF9, 0x91, 0x35, 0xB9, 0x08 });

			EcasActionType found = pool.FindAction(closeUuid);
			Assert.NotNull(found);
		}

		[Fact]
		public void EcasPool_WithDefaultProviders_FindsGlobalAutoTypeAction()
		{
			var pool = new EcasPool(bAddDefaultProviders: true);

			var autoTypeUuid = new PwUuid(new byte[] {
				0xA8, 0xA0, 0x64, 0x6D, 0xCE, 0x10, 0x40, 0xC1,
				0xB0, 0x89, 0xBA, 0x5A, 0x3B, 0xF7, 0xF6, 0xFE });

			EcasActionType found = pool.FindAction(autoTypeUuid);
			Assert.NotNull(found);
		}

		[Fact]
		public void EcasPool_WithoutDefaultProviders_DoesNotFindSaveDatabaseAction()
		{
			var pool = new EcasPool(bAddDefaultProviders: false);

			var saveUuid = new PwUuid(new byte[] {
				0x5A, 0x71, 0xB0, 0xAB, 0xB6, 0x78, 0x49, 0x6A,
				0x83, 0xCB, 0xBB, 0x42, 0x2B, 0xF2, 0x5B, 0xDB });

			EcasActionType found = pool.FindAction(saveUuid);
			Assert.Null(found);
		}

		// ── IUICommandService contract ─────────────────────────────────────────

		[Fact]
		public void IUICommandService_AllMembersCallable()
		{
			IUICommandService svc = new RecordingUiService();

			IOConnectionInfo ioc = new IOConnectionInfo { Path = "test.kdbx" };
			svc.OpenDatabase(ioc, null, true);
			svc.SaveActiveDatabase();
			svc.CloseActiveDatabase(false);
			svc.GetActiveDatabase();
			svc.GetDocumentManager();
			svc.MakeDocumentActive(null);
			svc.GetSelectedEntry(false);
			svc.ShowEntriesByTag("Work");
			svc.AddCustomToolBarButton("id1", "name", "desc");
			svc.RemoveCustomToolBarButton("id1");
			svc.SetInteractionBlocked(true);
			svc.CompleteConnectionInfoUsingMru(ioc);
			svc.ExecuteGlobalAutoType();

			var rec = (RecordingUiService)svc;
			Assert.Equal(13, rec.Calls.Count);
		}

		[Fact]
		public void IUICommandService_Stub_OpenDatabase_RecordsCall()
		{
			var svc = new RecordingUiService();
			svc.OpenDatabase(new IOConnectionInfo { Path = "x.kdbx" }, null, false);
			Assert.Contains("OpenDatabase", svc.Calls);
		}

		[Fact]
		public void IUICommandService_Stub_SaveActiveDatabase_RecordsCall()
		{
			var svc = new RecordingUiService();
			svc.SaveActiveDatabase();
			Assert.Contains("SaveActiveDatabase", svc.Calls);
		}

		[Fact]
		public void IUICommandService_Stub_CloseActiveDatabase_RecordsFlag()
		{
			var svc = new RecordingUiService();
			svc.CloseActiveDatabase(ecas: true);
			Assert.Contains("CloseActiveDatabase:True", svc.Calls);
		}
	}
}
