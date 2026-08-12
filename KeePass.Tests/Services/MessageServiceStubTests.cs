using System;
using KeePass.Core.Services;
using Xunit;

namespace KeePass.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="TestMessageService"/> and
    /// <see cref="TestDialogService"/> stubs, and for the enums / DTO types
    /// defined in <c>KeePass.Core.Services</c>.
    ///
    /// These tests have no WinForms dependency and run on all platforms.
    /// </summary>
    public class MessageServiceStubTests
    {
        // ── TestMessageService ────────────────────────────────────────────

        [Fact]
        public void TestMessageService_ShowInfo_RecordsCall()
        {
            var svc = new TestMessageService();
            svc.ShowInfo("Hello", "Title");

            Assert.Single(svc.Calls);
            Assert.Equal("ShowInfo", svc.Calls[0].Method);
            Assert.Equal("Hello", svc.Calls[0].Message);
            Assert.Equal("Title", svc.Calls[0].Title);
        }

        [Fact]
        public void TestMessageService_ShowWarning_RecordsCall()
        {
            var svc = new TestMessageService();
            svc.ShowWarning("Warn");

            Assert.Single(svc.Calls);
            Assert.Equal("ShowWarning", svc.Calls[0].Method);
        }

        [Fact]
        public void TestMessageService_ShowError_RecordsCall()
        {
            var svc = new TestMessageService();
            svc.ShowError("Err");
            Assert.Equal("ShowError", svc.Calls[0].Method);
        }

        [Fact]
        public void TestMessageService_ShowFatal_RecordsCall()
        {
            var svc = new TestMessageService();
            svc.ShowFatal("Fatal");
            Assert.Equal("ShowFatal", svc.Calls[0].Method);
        }

        [Fact]
        public void TestMessageService_AskYesNo_DefaultsToTrue()
        {
            var svc = new TestMessageService();
            bool result = svc.AskYesNo("Question?");
            Assert.True(result);
            Assert.Equal("AskYesNo", svc.Calls[0].Method);
        }

        [Fact]
        public void TestMessageService_AskYesNo_ConfiguredToReturnFalse()
        {
            var svc = new TestMessageService { AskYesNoResult = false };
            bool result = svc.AskYesNo("Are you sure?");
            Assert.False(result);
        }

        [Fact]
        public void TestMessageService_Reset_ClearsCalls()
        {
            var svc = new TestMessageService();
            svc.ShowInfo("msg");
            svc.Reset();
            Assert.Empty(svc.Calls);
        }

        [Fact]
        public void TestMessageService_ImplementsIMessageService()
        {
            IMessageService svc = new TestMessageService();
            Assert.NotNull(svc);
        }

        [Fact]
        public void TestMessageService_MultipleCallsAreOrdered()
        {
            var svc = new TestMessageService();
            svc.ShowInfo("a");
            svc.ShowWarning("b");
            svc.ShowError("c");

            Assert.Equal(3, svc.Calls.Count);
            Assert.Equal("ShowInfo",    svc.Calls[0].Method);
            Assert.Equal("ShowWarning", svc.Calls[1].Method);
            Assert.Equal("ShowError",   svc.Calls[2].Method);
        }

        // ── TestDialogService ─────────────────────────────────────────────

        [Fact]
        public void TestDialogService_ShowOpenFileDialog_RecordsCallAndReturnsNull()
        {
            var svc = new TestDialogService();
            string result = svc.ShowOpenFileDialog("Open");
            Assert.Null(result);
            Assert.Single(svc.Calls);
            Assert.Equal("ShowOpenFileDialog", svc.Calls[0].Method);
        }

        [Fact]
        public void TestDialogService_ShowOpenFileDialog_ReturnsConfiguredValue()
        {
            var svc = new TestDialogService { OpenFileDialogResult = "/tmp/test.kdbx" };
            string result = svc.ShowOpenFileDialog("Open");
            Assert.Equal("/tmp/test.kdbx", result);
        }

        [Fact]
        public void TestDialogService_ShowSaveFileDialog_RecordsCall()
        {
            var svc = new TestDialogService();
            svc.ShowSaveFileDialog("Save", "*.kdbx", null, "new.kdbx");
            Assert.Equal("ShowSaveFileDialog", svc.Calls[0].Method);
        }

        [Fact]
        public void TestDialogService_ShowInputDialog_ReturnsNull_WhenNotConfigured()
        {
            var svc = new TestDialogService();
            string result = svc.ShowInputDialog("Enter name:");
            Assert.Null(result);
        }

        [Fact]
        public void TestDialogService_ShowInputDialog_ReturnsConfiguredValue()
        {
            var svc = new TestDialogService { InputDialogResult = "MyInput" };
            string result = svc.ShowInputDialog("Enter:");
            Assert.Equal("MyInput", result);
        }

        [Fact]
        public void TestDialogService_ShowTaskDialog_ReturnsMinusOneByDefault()
        {
            var svc = new TestDialogService();
            var model = new TaskDialogModel
            {
                MainInstruction = "Confirm?",
                Buttons = new[] { "Yes", "No" }
            };
            int result = svc.ShowTaskDialog(model);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void TestDialogService_ShowTaskDialog_ReturnsConfiguredButtonIndex()
        {
            var svc = new TestDialogService { TaskDialogResult = 0 };
            var model = new TaskDialogModel { Buttons = new[] { "OK" } };
            int result = svc.ShowTaskDialog(model);
            Assert.Equal(0, result);
        }

        [Fact]
        public void TestDialogService_ShowTaskDialog_SetsVerificationResult()
        {
            var svc = new TestDialogService { TaskDialogResult = 0 };
            var model = new TaskDialogModel
            {
                Buttons = new[] { "OK" },
                VerificationText = "Don't show again",
                VerificationChecked = true
            };
            svc.ShowTaskDialog(model);
            Assert.False(model.VerificationResult); // stub always returns false
        }

        [Fact]
        public void TestDialogService_Reset_ClearsCalls()
        {
            var svc = new TestDialogService();
            svc.ShowOpenFileDialog("x");
            svc.Reset();
            Assert.Empty(svc.Calls);
        }

        [Fact]
        public void TestDialogService_ImplementsIDialogService()
        {
            IDialogService svc = new TestDialogService();
            Assert.NotNull(svc);
        }

        // ── Enums & DTO ───────────────────────────────────────────────────

        [Fact]
        public void MessageDialogResult_Values_AreDistinct()
        {
            Assert.NotEqual(MessageDialogResult.OK,     MessageDialogResult.Cancel);
            Assert.NotEqual(MessageDialogResult.Yes,    MessageDialogResult.No);
            Assert.NotEqual(MessageDialogResult.Abort,  MessageDialogResult.Retry);
        }

        [Fact]
        public void MessageSeverity_Values_AreDistinct()
        {
            Assert.NotEqual(MessageSeverity.Info,    MessageSeverity.Warning);
            Assert.NotEqual(MessageSeverity.Warning, MessageSeverity.Error);
            Assert.NotEqual(MessageSeverity.Error,   MessageSeverity.Fatal);
        }

        [Fact]
        public void TaskDialogModel_DefaultValues_AreCorrect()
        {
            var m = new TaskDialogModel();
            Assert.Null(m.MainInstruction);
            Assert.Null(m.Content);
            Assert.Equal(MessageSeverity.Info, m.Severity);
            Assert.Equal(0,    m.DefaultButtonIndex);
            Assert.False(m.VerificationChecked);
            Assert.False(m.UseCommandLinks);
            Assert.False(m.VerificationResult);
        }

        [Fact]
        public void TaskDialogModel_CanSetAllProperties()
        {
            var m = new TaskDialogModel
            {
                MainInstruction   = "Main",
                Content           = "Body",
                Severity          = MessageSeverity.Warning,
                Buttons           = new[] { "A", "B" },
                DefaultButtonIndex = 1,
                FooterText        = "Footer",
                FooterSeverity    = MessageSeverity.Info,
                VerificationText  = "Verify",
                VerificationChecked = true,
                UseCommandLinks   = true,
            };

            Assert.Equal("Main",    m.MainInstruction);
            Assert.Equal("Body",    m.Content);
            Assert.Equal(MessageSeverity.Warning, m.Severity);
            Assert.Equal(2,         m.Buttons.Length);
            Assert.Equal(1,         m.DefaultButtonIndex);
            Assert.Equal("Footer",  m.FooterText);
            Assert.Equal("Verify",  m.VerificationText);
            Assert.True(m.VerificationChecked);
            Assert.True(m.UseCommandLinks);
        }
    }
}
