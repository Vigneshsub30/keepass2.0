#nullable enable

using System;
using System.Collections.Generic;

using KeePassLib;
using KeePassLib.Diagnostics;

using Xunit;

namespace KeePass.Tests.Platform
{
	/// <summary>
	/// Tests for <see cref="KeePassLogRedactor"/>.
	/// </summary>
	public sealed class KeePassLogRedactorTests
	{
		// ── IsVaultField ───────────────────────────────────────────── //

		[Theory]
		[InlineData(PwDefs.TitleField)]
		[InlineData(PwDefs.UserNameField)]
		[InlineData(PwDefs.PasswordField)]
		[InlineData(PwDefs.UrlField)]
		[InlineData(PwDefs.NotesField)]
		public void IsVaultField_StandardFields_ReturnsTrue(string fieldKey)
		{
			Assert.True(KeePassLogRedactor.IsVaultField(fieldKey));
		}

		[Theory]
		[InlineData("MyCustomField")]
		[InlineData("TOTP_Seed")]
		[InlineData("CreditCardNumber")]
		[InlineData("SecretPin")]
		[InlineData("pluginPath")]
		public void IsVaultField_NonStandardKeys_ReturnsFalse(string fieldKey)
		{
			// Non-standard keys are not automatically vault fields — callers
			// must use Redact() explicitly for custom field values.
			Assert.False(KeePassLogRedactor.IsVaultField(fieldKey));
		}

		[Fact]
		public void IsVaultField_NullFieldKey_ReturnsFalse()
		{
			Assert.False(KeePassLogRedactor.IsVaultField(null));
		}

		// ── RedactIfVaultField ─────────────────────────────────────── //

		[Theory]
		[InlineData(PwDefs.TitleField,    "My Bank Account")]
		[InlineData(PwDefs.UserNameField, "alice@example.com")]
		[InlineData(PwDefs.PasswordField, "P@ssw0rd!")]
		[InlineData(PwDefs.UrlField,      "https://bank.example.com")]
		[InlineData(PwDefs.NotesField,    "Top secret notes here")]
		public void RedactIfVaultField_StandardVaultContent_IsRedacted(
			string fieldKey, string value)
		{
			string result = KeePassLogRedactor.RedactIfVaultField(fieldKey, value);
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, result);
		}

		[Theory]
		[InlineData("MyCustomField", "super secret")]
		[InlineData("TOTP_Seed",    "JBSWY3DPEHPK3PXP")]
		public void RedactIfVaultField_CustomNonVaultKey_PassesThrough(
			string fieldKey, string value)
		{
			// Custom keys are NOT automatically treated as vault fields — use
			// Redact() explicitly for custom vault field values.
			string result = KeePassLogRedactor.RedactIfVaultField(fieldKey, value);
			Assert.Equal(value, result);
		}

		[Fact]
		public void Redact_ExplicitlyRedactsCustomVaultContent()
		{
			// Callers handling custom vault fields should use Redact() directly.
			string result = KeePassLogRedactor.Redact("JBSWY3DPEHPK3PXP");
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, result);
		}

		[Fact]
		public void RedactIfVaultField_NullFieldKey_ReturnsRawValue()
		{
			// null fieldKey signals the value is not from a vault field.
			const string rawValue = "path/to/plugin.dll";
			string result = KeePassLogRedactor.RedactIfVaultField(null, rawValue);
			Assert.Equal(rawValue, result);
		}

		[Fact]
		public void RedactIfVaultField_NullValue_WithNullFieldKey_ReturnsEmpty()
		{
			string result = KeePassLogRedactor.RedactIfVaultField(null, null);
			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void RedactIfVaultField_NullValue_WithVaultFieldKey_ReturnsRedacted()
		{
			string result = KeePassLogRedactor.RedactIfVaultField(PwDefs.PasswordField, null);
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, result);
		}

		[Fact]
		public void RedactIfVaultField_EmptyValue_NonVaultKey_ReturnsEmpty()
		{
			// Non-vault keys (null) pass through even when value is empty.
			string result = KeePassLogRedactor.RedactIfVaultField(null, string.Empty);
			Assert.Equal(string.Empty, result);
		}

		// ── Redact ────────────────────────────────────────────────── //

		[Fact]
		public void Redact_AlwaysReturnsPlaceholder()
		{
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, KeePassLogRedactor.Redact("anything"));
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, KeePassLogRedactor.Redact(null));
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, KeePassLogRedactor.Redact());
		}

		// ── Non-sensitive passthrough ──────────────────────────────── //

		[Theory]
		[InlineData(null, "/path/to/MyPlugin.dll")]
		[InlineData(null, "PluginManager")]
		[InlineData(null, "IOException: disk full")]
		[InlineData(null, "42")]
		public void RedactIfVaultField_NonVaultContent_PassesThrough(
			string? fieldKey, string value)
		{
			// Non-vault parameters (no field key) must pass through unchanged
			// so that useful operational data is not silently removed.
			string result = KeePassLogRedactor.RedactIfVaultField(fieldKey, value);
			Assert.Equal(value, result);
		}

		// ── RedactParameters (dictionary) ──────────────────────────── //

		[Fact]
		public void RedactParameters_VaultKeys_AreRedacted()
		{
			var parameters = new Dictionary<string, string?>
			{
				{ PwDefs.TitleField,    "My Account" },
				{ PwDefs.PasswordField, "secret"     },
				{ "pluginPath",         "/plugins/foo.dll" },
			};

			KeePassLogRedactor.RedactParameters(parameters);

			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, parameters[PwDefs.TitleField]);
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, parameters[PwDefs.PasswordField]);
			// Non-vault parameter must remain intact.
			Assert.Equal("/plugins/foo.dll", parameters["pluginPath"]);
		}

		[Fact]
		public void RedactParameters_EmptyDictionary_DoesNotThrow()
		{
			var parameters = new Dictionary<string, string?>();
			KeePassLogRedactor.RedactParameters(parameters); // must not throw
			Assert.Empty(parameters);
		}

		[Fact]
		public void RedactParameters_NullDictionary_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() =>
				KeePassLogRedactor.RedactParameters(null!));
		}

		[Fact]
		public void RedactParameters_NonStandardKey_PassesThrough()
		{
			// Custom keys (e.g. "TOTP_Seed", "pluginName") are not auto-redacted
			// by RedactParameters — only standard vault field keys are.
			var parameters = new Dictionary<string, string?>
			{
				{ "TOTP_Seed",  "JBSWY3DPEHPK3PXP" },
				{ "pluginName", "SomePlugin" },
			};

			KeePassLogRedactor.RedactParameters(parameters);

			Assert.Equal("JBSWY3DPEHPK3PXP", parameters["TOTP_Seed"]);
			Assert.Equal("SomePlugin",       parameters["pluginName"]);
		}

		// ── Very long strings ──────────────────────────────────────── //

		[Fact]
		public void RedactIfVaultField_VeryLongPasswordValue_IsRedacted()
		{
			string longPassword = new string('X', 100_000);
			string result = KeePassLogRedactor.RedactIfVaultField(PwDefs.PasswordField, longPassword);
			Assert.Equal(KeePassLogRedactor.RedactedPlaceholder, result);
		}

		[Fact]
		public void RedactIfVaultField_VeryLongNonVaultValue_PassesThrough()
		{
			string longPath = "/" + new string('a', 100_000);
			string result = KeePassLogRedactor.RedactIfVaultField(null, longPath);
			Assert.Equal(longPath, result);
		}
	}
}
