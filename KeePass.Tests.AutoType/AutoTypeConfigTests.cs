#nullable enable

using System;
using System.Collections.Generic;

using KeePassLib.Collections;

using Xunit;

namespace KeePass.Tests.AutoType
{
	/// <summary>
	/// Tests for the <see cref="AutoTypeConfig"/> and
	/// <see cref="AutoTypeAssociation"/> data models.
	/// These models live in KeePassLib and are platform-neutral.
	/// </summary>
	public sealed class AutoTypeConfigTests
	{
		// ── AutoTypeAssociation ────────────────────────────────────────── //

		[Fact]
		public void AutoTypeAssociation_DefaultCtor_EmptyStrings()
		{
			var a = new AutoTypeAssociation();
			Assert.Equal(string.Empty, a.WindowName);
			Assert.Equal(string.Empty, a.Sequence);
		}

		[Fact]
		public void AutoTypeAssociation_Ctor_SetsProperties()
		{
			var a = new AutoTypeAssociation("Notepad", "{USERNAME}{TAB}{PASSWORD}{ENTER}");
			Assert.Equal("Notepad", a.WindowName);
			Assert.Equal("{USERNAME}{TAB}{PASSWORD}{ENTER}", a.Sequence);
		}

		[Fact]
		public void AutoTypeAssociation_Equality_SameValues_Equal()
		{
			var a = new AutoTypeAssociation("Window", "Seq");
			var b = new AutoTypeAssociation("Window", "Seq");
			Assert.True(a.Equals(b));
		}

		[Fact]
		public void AutoTypeAssociation_Equality_DifferentWindow_NotEqual()
		{
			var a = new AutoTypeAssociation("Window1", "Seq");
			var b = new AutoTypeAssociation("Window2", "Seq");
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void AutoTypeAssociation_Equality_DifferentSequence_NotEqual()
		{
			var a = new AutoTypeAssociation("Window", "Seq1");
			var b = new AutoTypeAssociation("Window", "Seq2");
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void AutoTypeAssociation_CloneDeep_IndependentCopy()
		{
			var original = new AutoTypeAssociation("Notepad", "{ENTER}");
			AutoTypeAssociation clone = original.CloneDeep();
			clone.WindowName = "Chrome";
			Assert.Equal("Notepad", original.WindowName);
		}

		[Fact]
		public void AutoTypeAssociation_NullWindowName_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AutoTypeAssociation(null!, "{ENTER}"));
		}

		[Fact]
		public void AutoTypeAssociation_NullSequence_Throws()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new AutoTypeAssociation("Notepad", null!));
		}

		// ── AutoTypeConfig ─────────────────────────────────────────────── //

		[Fact]
		public void AutoTypeConfig_DefaultEnabled_IsTrue()
		{
			var cfg = new AutoTypeConfig();
			Assert.True(cfg.Enabled);
		}

		[Fact]
		public void AutoTypeConfig_DefaultSequence_Empty()
		{
			var cfg = new AutoTypeConfig();
			Assert.Equal(string.Empty, cfg.DefaultSequence);
		}

		[Fact]
		public void AutoTypeConfig_DefaultObfuscation_None()
		{
			var cfg = new AutoTypeConfig();
			Assert.Equal(AutoTypeObfuscationOptions.None, cfg.ObfuscationOptions);
		}

		[Fact]
		public void AutoTypeConfig_AddAssociation_CountIncreases()
		{
			var cfg = new AutoTypeConfig();
			cfg.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));
			Assert.Equal(1, cfg.AssociationsCount);
		}

		[Fact]
		public void AutoTypeConfig_AddMultipleAssociations()
		{
			var cfg = new AutoTypeConfig();
			cfg.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));
			cfg.Add(new AutoTypeAssociation("Chrome", "{USERNAME}{TAB}{PASSWORD}"));
			cfg.Add(new AutoTypeAssociation("Firefox", "{USERNAME}{TAB}{PASSWORD}{ENTER}"));
			Assert.Equal(3, cfg.AssociationsCount);
		}

		[Fact]
		public void AutoTypeConfig_Equality_SameState_Equal()
		{
			var a = new AutoTypeConfig();
			a.DefaultSequence = "{USERNAME}";
			a.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));

			var b = new AutoTypeConfig();
			b.DefaultSequence = "{USERNAME}";
			b.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));

			Assert.True(a.Equals(b));
		}

		[Fact]
		public void AutoTypeConfig_Equality_DifferentEnabled_NotEqual()
		{
			var a = new AutoTypeConfig { Enabled = true };
			var b = new AutoTypeConfig { Enabled = false };
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void AutoTypeConfig_Equality_DifferentSequence_NotEqual()
		{
			var a = new AutoTypeConfig { DefaultSequence = "{USERNAME}" };
			var b = new AutoTypeConfig { DefaultSequence = "{PASSWORD}" };
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void AutoTypeConfig_CloneDeep_IndependentCopy()
		{
			var original = new AutoTypeConfig();
			original.DefaultSequence = "{USERNAME}";
			original.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));

			AutoTypeConfig clone = original.CloneDeep();
			clone.DefaultSequence = "{MODIFIED}";
			Assert.Equal("{USERNAME}", original.DefaultSequence);
		}

		[Fact]
		public void AutoTypeConfig_CloneDeep_AssociationsAreCloned()
		{
			var original = new AutoTypeConfig();
			original.Add(new AutoTypeAssociation("Notepad", "{ENTER}"));

			AutoTypeConfig clone = original.CloneDeep();
			clone.GetAt(0).WindowName = "Chrome";
			Assert.Equal("Notepad", original.GetAt(0).WindowName);
		}
	}
}
