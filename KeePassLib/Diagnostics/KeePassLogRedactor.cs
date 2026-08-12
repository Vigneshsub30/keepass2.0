// Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;

using KeePassLib;

namespace KeePassLib.Diagnostics
{
	/// <summary>
	/// Redacts vault content values from structured log parameters to prevent
	/// sensitive data (entry titles, usernames, passwords, URLs, notes, and
	/// custom field values) from appearing in diagnostic log output.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The redactor is the authoritative gate between application code and the
	/// logging sink.  All structured log parameters that may originate from
	/// vault entry fields MUST pass through <see cref="RedactIfVaultField"/>
	/// before being forwarded to an <c>ILogger</c>.
	/// </para>
	/// <para>
	/// <b>What is redacted:</b> values associated with any of the five standard
	/// KeePass field names (<c>Title</c>, <c>UserName</c>, <c>Password</c>,
	/// <c>URL</c>, <c>Notes</c>) and any non-standard (custom) field key, since
	/// custom field values are also considered vault content.
	/// </para>
	/// <para>
	/// <b>What is NOT redacted:</b> file paths, plugin names, exception messages,
	/// assembly names, counts, timestamps, durations — any value that is not
	/// directly tied to a vault entry field key.
	/// </para>
	/// </remarks>
	public static class KeePassLogRedactor
	{
		/// <summary>Placeholder substituted for every redacted value.</summary>
		public const string RedactedPlaceholder = "[REDACTED]";

		/// <summary>
		/// Returns <paramref name="value"/> if <paramref name="fieldKey"/> is
		/// not a vault field name; otherwise returns <see cref="RedactedPlaceholder"/>.
		/// </summary>
		/// <param name="fieldKey">
		/// The KeePass field key, e.g. <c>PwDefs.TitleField</c> or a custom key.
		/// Pass <c>null</c> to indicate the value does not come from a vault field;
		/// in that case the raw value is returned unchanged.
		/// </param>
		/// <param name="value">The candidate log parameter value.</param>
		/// <returns>
		/// The original <paramref name="value"/> or <see cref="RedactedPlaceholder"/>.
		/// </returns>
		public static string RedactIfVaultField(string? fieldKey, string? value)
		{
			if(fieldKey == null) return value ?? string.Empty;
			if(IsVaultField(fieldKey)) return RedactedPlaceholder;
			return value ?? string.Empty;
		}

		/// <summary>
		/// Returns <see cref="RedactedPlaceholder"/> for any string value that
		/// is associated with vault content.  Use for parameters that are always
		/// sensitive regardless of their originating field key (e.g. the raw
		/// value read from a <c>ProtectedString</c>).
		/// </summary>
		public static string Redact(string? _ = null) => RedactedPlaceholder;

		/// <summary>
		/// Returns <c>true</c> if <paramref name="fieldKey"/> refers to a vault
		/// content field whose value must be redacted from log output.
		/// </summary>
		/// <remarks>
		/// The five standard KeePass fields (<c>Title</c>, <c>UserName</c>,
		/// <c>Password</c>, <c>URL</c>, <c>Notes</c>) are automatically detected
		/// as vault fields.  For custom vault field values (e.g. TOTP seeds,
		/// credit card numbers) callers should use <see cref="Redact"/> directly,
		/// since the redactor cannot distinguish a custom vault field key from an
		/// arbitrary structured log parameter key at the dictionary level.
		/// </remarks>
		public static bool IsVaultField(string? fieldKey)
		{
			if(fieldKey == null) return false;
			return fieldKey == PwDefs.TitleField    ||
			       fieldKey == PwDefs.UserNameField ||
			       fieldKey == PwDefs.PasswordField ||
			       fieldKey == PwDefs.UrlField      ||
			       fieldKey == PwDefs.NotesField;
		}

		/// <summary>
		/// Sanitises a dictionary of structured log parameters in-place, replacing
		/// values that originate from vault fields with <see cref="RedactedPlaceholder"/>.
		/// </summary>
		/// <param name="parameters">
		/// A key-value map of log parameters.  Keys that match vault field names
		/// (or any non-standard field) are redacted.
		/// </param>
		public static void RedactParameters(IDictionary<string, string?> parameters)
		{
			if(parameters == null) throw new ArgumentNullException(nameof(parameters));

			// Collect keys first to avoid modifying the dictionary while iterating.
			List<string> keysToRedact = null!;
			foreach(string key in parameters.Keys)
			{
				if(!IsVaultField(key)) continue;
				if(keysToRedact == null) keysToRedact = new List<string>();
				keysToRedact.Add(key);
			}

			if(keysToRedact == null) return;
			foreach(string key in keysToRedact)
				parameters[key] = RedactedPlaceholder;
		}
	}
}
