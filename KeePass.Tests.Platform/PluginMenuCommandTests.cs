#nullable enable

using System;
using System.Collections.Generic;

using KeePassLib.Plugins;

using Xunit;

namespace KeePass.Tests.Platform
{
	public sealed class PluginMenuCommandTests
	{
		[Fact]
		public void Constructor_SetsText()
		{
			var cmd = new PluginMenuCommand("Open");
			Assert.Equal("Open", cmd.Text);
		}

		[Fact]
		public void Constructor_NullText_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new PluginMenuCommand(null!));
		}

		[Fact]
		public void Constructor_ClickHandler_IsSet()
		{
			EventHandler handler = (s, e) => { };
			var cmd = new PluginMenuCommand("Action", handler);
			Assert.Same(handler, cmd.Click);
		}

		[Fact]
		public void DefaultValues_EnabledTrue_CheckedFalse_NotSeparator()
		{
			var cmd = new PluginMenuCommand("Item");
			Assert.True(cmd.Enabled);
			Assert.False(cmd.Checked);
			Assert.False(cmd.IsSeparator);
		}

		[Fact]
		public void ToolTipText_CanBeSet()
		{
			var cmd = new PluginMenuCommand("Item") { ToolTipText = "tip" };
			Assert.Equal("tip", cmd.ToolTipText);
		}

		[Fact]
		public void ShortcutKeyDescription_CanBeSet()
		{
			var cmd = new PluginMenuCommand("Item") { ShortcutKeyDescription = "Ctrl+T" };
			Assert.Equal("Ctrl+T", cmd.ShortcutKeyDescription);
		}

		[Fact]
		public void ImageData_NullByDefault()
		{
			var cmd = new PluginMenuCommand("Item");
			Assert.Null(cmd.ImageData);
		}

		[Fact]
		public void ImageData_CanBeAssigned()
		{
			byte[] data = new byte[] { 1, 2, 3 };
			var cmd = new PluginMenuCommand("Item") { ImageData = data };
			Assert.Same(data, cmd.ImageData);
		}

		[Fact]
		public void SubItems_EmptyByDefault()
		{
			var cmd = new PluginMenuCommand("Item");
			Assert.Empty(cmd.SubItems);
		}

		[Fact]
		public void SubItems_RecursiveChildren_CanBeTraversed()
		{
			var child1 = new PluginMenuCommand("Child 1");
			var child2 = new PluginMenuCommand("Child 2");
			var grandchild = new PluginMenuCommand("Grandchild");
			child1.SubItems.Add(grandchild);

			var parent = new PluginMenuCommand("Parent");
			parent.SubItems.Add(child1);
			parent.SubItems.Add(child2);

			Assert.Equal(2, parent.SubItems.Count);
			Assert.Single(parent.SubItems[0].SubItems);
			Assert.Equal("Grandchild", parent.SubItems[0].SubItems[0].Text);
		}

		[Fact]
		public void Separator_Factory_IsSeparatorTrue()
		{
			PluginMenuCommand sep = PluginMenuCommand.Separator();
			Assert.True(sep.IsSeparator);
		}

		[Fact]
		public void Enabled_CanBeSetFalse()
		{
			var cmd = new PluginMenuCommand("Disabled") { Enabled = false };
			Assert.False(cmd.Enabled);
		}

		[Fact]
		public void Checked_CanBeSetTrue()
		{
			var cmd = new PluginMenuCommand("Checked") { Checked = true };
			Assert.True(cmd.Checked);
		}

		[Fact]
		public void ShortcutKeyDescription_NullByDefault()
		{
			var cmd = new PluginMenuCommand("Item");
			Assert.Null(cmd.ShortcutKeyDescription);
		}
	}
}
