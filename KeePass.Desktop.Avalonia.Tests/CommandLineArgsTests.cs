#nullable enable

using KeePass.Core.Services;

using Xunit;

namespace KeePass.Desktop.Avalonia.Tests
{
	public sealed class CommandLineArgsTests
	{
		[Fact]
		public void Parse_EmptyArgs_FilePath_IsNull()
		{
			var args = CommandLineArgs.Parse(new string[0]);
			Assert.Null(args.FilePath);
			Assert.Empty(args.ExtraArgs);
		}

		[Fact]
		public void Parse_AbsoluteKdbxPath_ExtractedAsFilePath()
		{
			var args = CommandLineArgs.Parse(new[] { "/home/user/my vault.kdbx" });
			Assert.Equal("/home/user/my vault.kdbx", args.FilePath);
		}

		[Fact]
		public void Parse_WindowsAbsolutePath_ExtractedAsFilePath()
		{
			var args = CommandLineArgs.Parse(new[] { @"C:\Users\User\Vault.kdbx" });
			Assert.Equal(@"C:\Users\User\Vault.kdbx", args.FilePath);
		}

		[Fact]
		public void Parse_RelativeKdbxPath_ExtractedAsFilePath()
		{
			var args = CommandLineArgs.Parse(new[] { "vault.kdbx" });
			Assert.Equal("vault.kdbx", args.FilePath);
		}

		[Fact]
		public void Parse_KdbxExtensionCaseInsensitive()
		{
			var args = CommandLineArgs.Parse(new[] { "VAULT.KDBX" });
			Assert.Equal("VAULT.KDBX", args.FilePath);
		}

		[Fact]
		public void Parse_PathWithSpaces_ExtractedAsFilePath()
		{
			var args = CommandLineArgs.Parse(new[] { "/my documents/my passwords.kdbx" });
			Assert.Equal("/my documents/my passwords.kdbx", args.FilePath);
		}

		[Fact]
		public void Parse_PathWithUnicode_ExtractedAsFilePath()
		{
			const string path = "/home/tëst/päss wörds.kdbx";
			var args = CommandLineArgs.Parse(new[] { path });
			Assert.Equal(path, args.FilePath);
		}

		[Fact]
		public void Parse_MultipleKdbxPaths_LastOneWins()
		{
			var args = CommandLineArgs.Parse(new[]
			{
				"/first.kdbx",
				"/second.kdbx"
			});
			Assert.Equal("/second.kdbx", args.FilePath);
		}

		[Fact]
		public void Parse_NonKdbxArgs_GoToExtraArgs()
		{
			var args = CommandLineArgs.Parse(new[] { "--minimized", "-lock" });
			Assert.Null(args.FilePath);
			Assert.Equal(2, args.ExtraArgs.Count);
		}

		[Fact]
		public void Parse_MixedArgs_SeparatesCorrectly()
		{
			var args = CommandLineArgs.Parse(new[]
			{
				"--minimized",
				"/home/user/vault.kdbx",
				"--no-splash"
			});
			Assert.Equal("/home/user/vault.kdbx", args.FilePath);
			Assert.Equal(2, args.ExtraArgs.Count);
			Assert.Contains("--minimized", args.ExtraArgs);
			Assert.Contains("--no-splash", args.ExtraArgs);
		}

		[Fact]
		public void Parse_NullArgs_ThrowsArgumentNullException()
		{
			Assert.Throws<System.ArgumentNullException>(
				() => CommandLineArgs.Parse(null!));
		}

		[Fact]
		public void IsKdbxPath_NullOrEmpty_ReturnsFalse()
		{
			Assert.False(CommandLineArgs.IsKdbxPath(null!));
			Assert.False(CommandLineArgs.IsKdbxPath(""));
		}

		[Fact]
		public void IsKdbxPath_NonKdbx_ReturnsFalse()
		{
			Assert.False(CommandLineArgs.IsKdbxPath("vault.kdb"));
			Assert.False(CommandLineArgs.IsKdbxPath("--flag"));
			Assert.False(CommandLineArgs.IsKdbxPath(".kdbx.bak"));
		}

		[Fact]
		public void IsKdbxPath_KdbxExtension_ReturnsTrue()
		{
			Assert.True(CommandLineArgs.IsKdbxPath("vault.kdbx"));
			Assert.True(CommandLineArgs.IsKdbxPath("VAULT.KDBX"));
			Assert.True(CommandLineArgs.IsKdbxPath("/abs/path/vault.kdbx"));
		}
	}
}
