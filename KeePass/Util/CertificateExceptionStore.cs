// Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

using KeePass.App.Configuration;

namespace KeePass.Util
{
	/// <summary>
	/// Manages per-host TLS certificate exceptions, replacing the old global
	/// <c>ServicePointManager.ServerCertificateValidationCallback</c> bypass.
	///
	/// An exception allows one specific certificate (identified by its SHA-256
	/// thumbprint) to be used by one specific host even when the system TLS policy
	/// would normally reject it (e.g. self-signed CA, expired cert, hostname mismatch).
	///
	/// Entries are persisted via <see cref="AceSecurity.CertificateExceptions"/>
	/// as <c>host::thumbprint</c> strings (host = lowercase, thumbprint = lowercase
	/// hex, no colons).
	/// </summary>
	public static class CertificateExceptionStore
	{
		private const char Separator = ':'; // stored as host::thumbprint (double-colon)

		// ── Query ─────────────────────────────────────────────────────── //

		/// <summary>
		/// Returns <c>true</c> if the given host+thumbprint pair has been
		/// explicitly allowed by the user.
		/// </summary>
		/// <param name="host">
		/// The HTTPS host name (case-insensitive; leading/trailing whitespace
		/// is stripped).
		/// </param>
		/// <param name="sha256Thumbprint">
		/// The SHA-256 thumbprint of the certificate, as a lowercase hex string
		/// without colons (e.g. <c>"a3b4c5..."</c>).
		/// </param>
		/// <param name="security">
		/// The application security configuration that holds the persisted list.
		/// </param>
		public static bool IsAllowed(string host, string sha256Thumbprint,
			AceSecurity security)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));
			if(sha256Thumbprint == null)
				throw new ArgumentNullException(nameof(sha256Thumbprint));
			if(security == null) throw new ArgumentNullException(nameof(security));

			string entry = BuildEntry(host, sha256Thumbprint);
			foreach(string stored in security.CertificateExceptions)
			{
				if(string.Equals(stored, entry, StringComparison.Ordinal))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Returns <c>true</c> when the supplied host has at least one exception
		/// stored but with a <em>different</em> thumbprint — indicating the
		/// certificate has changed since it was last accepted.
		/// </summary>
		public static bool IsCertificateChanged(string host, string sha256Thumbprint,
			AceSecurity security)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));
			if(sha256Thumbprint == null)
				throw new ArgumentNullException(nameof(sha256Thumbprint));
			if(security == null) throw new ArgumentNullException(nameof(security));

			string prefix = NormaliseHost(host) + Separator.ToString() + Separator.ToString();
			string expectedEntry = BuildEntry(host, sha256Thumbprint);

			bool hasPreviousEntry = false;
			foreach(string stored in security.CertificateExceptions)
			{
				if(!stored.StartsWith(prefix, StringComparison.Ordinal)) continue;
				hasPreviousEntry = true;
				if(!string.Equals(stored, expectedEntry, StringComparison.Ordinal))
					return true; // Same host, different thumbprint → changed
			}
			return false; // Either no prior entry or thumbprint matches
		}

		// ── Mutation ───────────────────────────────────────────────────── //

		/// <summary>
		/// Adds a host+thumbprint exception.  If an entry for the same host
		/// already exists it is replaced (the certificate may have changed and
		/// the user explicitly re-accepted it).
		/// </summary>
		public static void Add(string host, string sha256Thumbprint,
			AceSecurity security)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));
			if(sha256Thumbprint == null)
				throw new ArgumentNullException(nameof(sha256Thumbprint));
			if(security == null) throw new ArgumentNullException(nameof(security));

			string normHost = NormaliseHost(host);
			string prefix   = normHost + Separator.ToString() + Separator.ToString();
			string newEntry = BuildEntry(host, sha256Thumbprint);

			List<string> list = security.CertificateExceptions;

			// Remove any existing entries for this host (handles cert rotation).
			for(int i = list.Count - 1; i >= 0; i--)
			{
				if(list[i].StartsWith(prefix, StringComparison.Ordinal))
					list.RemoveAt(i);
			}

			list.Add(newEntry);
		}

		/// <summary>
		/// Removes all stored exceptions for the given host.
		/// </summary>
		public static void Remove(string host, AceSecurity security)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));
			if(security == null) throw new ArgumentNullException(nameof(security));

			string prefix = NormaliseHost(host) + Separator.ToString() + Separator.ToString();
			List<string> list = security.CertificateExceptions;
			for(int i = list.Count - 1; i >= 0; i--)
			{
				if(list[i].StartsWith(prefix, StringComparison.Ordinal))
					list.RemoveAt(i);
			}
		}

		// ── Certificate helper ─────────────────────────────────────────── //

		/// <summary>
		/// Returns the SHA-256 thumbprint of an X.509 certificate as a lowercase
		/// hex string without colon separators, suitable for storage and lookup.
		/// </summary>
		public static string GetSha256Thumbprint(X509Certificate certificate)
		{
			if(certificate == null) throw new ArgumentNullException(nameof(certificate));

			byte[] raw = certificate.GetRawCertData();
			using System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create();
			byte[] hash = sha.ComputeHash(raw);
			return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
		}

		// ── Private helpers ────────────────────────────────────────────── //

		private static string NormaliseHost(string host) =>
			(host ?? string.Empty).Trim().ToLowerInvariant();

		private static string NormaliseThumbprint(string thumbprint) =>
			(thumbprint ?? string.Empty).Trim()
				.Replace(":", string.Empty)
				.Replace(" ", string.Empty)
				.ToLowerInvariant();

		private static string BuildEntry(string host, string thumbprint) =>
			NormaliseHost(host) + Separator.ToString() + Separator.ToString() +
			NormaliseThumbprint(thumbprint);
	}
}
