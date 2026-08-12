// Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.

using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

using KeePass.App.Configuration;
using KeePass.Forms;

namespace KeePass.Util
{
	/// <summary>
	/// Provides the per-connection <c>ServerCertificateCustomValidationCallback</c>
	/// that replaces the old global <c>ServicePointManager</c> bypass.
	///
	/// When a certificate is considered invalid by the system TLS stack, this
	/// handler checks the <see cref="CertificateExceptionStore"/>.  If a matching
	/// exception exists the connection proceeds; otherwise the user is shown
	/// <see cref="CertificateExceptionForm"/> to decide.
	/// </summary>
	public static class CertificateValidationHandler
	{
		/// <summary>
		/// Factory method: returns a callback delegate suitable for
		/// <c>HttpClientHandler.ServerCertificateCustomValidationCallback</c> or
		/// <c>HttpWebRequest.ServerCertificateValidationCallback</c> for the given
		/// <paramref name="host"/>.
		/// </summary>
		/// <param name="host">
		/// The host name of the HTTPS endpoint (used for per-host exception lookup).
		/// </param>
		/// <param name="parentForm">
		/// Optional WinForms parent for the certificate prompt.  Pass <c>null</c>
		/// when no UI parent is available.
		/// </param>
		public static Func<HttpRequestMessage, X509Certificate2, X509Chain,
			SslPolicyErrors, bool> CreateCallback(string host, Form? parentForm = null)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));

			return (message, certificate, chain, errors) =>
				Validate(host, certificate, errors, parentForm);
		}

		/// <summary>
		/// Core validation logic.  Returns <c>true</c> to allow the connection
		/// or <c>false</c> to abort it.
		///
		/// Decision order:
		/// <list type="number">
		///   <item>No TLS errors → allow (normal case).</item>
		///   <item>Matching per-host exception in store → allow.</item>
		///   <item>Certificate changed for a known host → prompt with change warning.</item>
		///   <item>Unknown invalid certificate → prompt.</item>
		///   <item>Prompt accepted for this session → allow (no persistence).</item>
		///   <item>Prompt accepted always → store exception and allow.</item>
		///   <item>Prompt rejected → deny.</item>
		/// </list>
		/// </summary>
		internal static bool Validate(string host, X509Certificate certificate,
			SslPolicyErrors errors, Form? parentForm)
		{
			// Fast path: certificate is valid per system policy.
			if(errors == SslPolicyErrors.None) return true;

			AceSecurity security = Program.Config?.Security;
			if(security == null) return false; // Deny when config unavailable.

			string thumbprint = CertificateExceptionStore.GetSha256Thumbprint(certificate);

			// Check persistent per-host exception.
			if(CertificateExceptionStore.IsAllowed(host, thumbprint, security))
				return true;

			// Detect certificate rotation.
			bool certChanged = CertificateExceptionStore.IsCertificateChanged(
				host, thumbprint, security);

			// Prompt the user — must run on the UI thread.
			DialogResult dr = DialogResult.Cancel;
			string? acceptedThumbprint = null;

			void ShowDialog()
			{
				using CertificateExceptionForm form = new CertificateExceptionForm(
					host, certificate, errors, certChanged);
				dr = form.ShowDialog(parentForm);
				acceptedThumbprint = form.AcceptedThumbprint;
			}

			MainForm? mf = Program.MainForm;
			if(mf != null && mf.InvokeRequired)
				mf.Invoke((MethodInvoker)ShowDialog);
			else
				ShowDialog();

			if(dr == DialogResult.Cancel) return false; // Rejected.

			// DialogResult.OK = "Accept Always" → persist.
			if(dr == DialogResult.OK && acceptedThumbprint != null)
			{
				CertificateExceptionStore.Add(host, acceptedThumbprint, security);
				// Persist immediately so a subsequent connection in the same session
				// does not prompt again.
				try { App.Configuration.AppConfigSerializer.Save(); }
				catch(Exception) { /* non-critical */ }
			}

			// DialogResult.Yes = "Accept (This Session Only)" → allow without persisting.
			return true;
		}
	}
}
