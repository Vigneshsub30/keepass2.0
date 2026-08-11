// Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.

using System;
using System.Drawing;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

using KeePass.UI;
using KeePass.Util;

namespace KeePass.Forms
{
	/// <summary>
	/// Presents an invalid TLS certificate to the user and asks whether to
	/// accept it for the current host.  Shown by
	/// <see cref="KeePass.Util.CertificateExceptionDialog.Show"/> when an
	/// HTTPS connection encounters a certificate that does not pass the default
	/// system policy.
	/// </summary>
	public sealed class CertificateExceptionForm : Form
	{
		private readonly string m_strHost;
		private readonly X509Certificate m_cert;
		private readonly SslPolicyErrors m_errors;
		private readonly bool m_bCertChanged;

		/// <summary>
		/// Gets the thumbprint of the certificate the user was shown.
		/// Available after the form is closed.
		/// </summary>
		public string AcceptedThumbprint { get; private set; }

		/// <param name="host">HTTPS host name (display purposes).</param>
		/// <param name="certificate">Certificate presented by the server.</param>
		/// <param name="errors">TLS policy errors reported by the runtime.</param>
		/// <param name="certChanged">
		/// <c>true</c> when the user has previously accepted a certificate for
		/// this host but the certificate has since changed.
		/// </param>
		public CertificateExceptionForm(string host, X509Certificate certificate,
			SslPolicyErrors errors, bool certChanged)
		{
			if(host == null) throw new ArgumentNullException(nameof(host));
			if(certificate == null) throw new ArgumentNullException(nameof(certificate));

			m_strHost   = host;
			m_cert      = certificate;
			m_errors    = errors;
			m_bCertChanged = certChanged;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			Text = "TLS Certificate Warning — KeePass";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition  = FormStartPosition.CenterParent;
			MinimizeBox    = false;
			MaximizeBox    = false;
			ShowInTaskbar  = false;
			Size           = new Size(520, 340);
			AutoScaleMode  = AutoScaleMode.Dpi;

			// ── Warning icon + header label ─────────────────────────── //
			PictureBox pbIcon = new PictureBox
			{
				Image    = SystemIcons.Warning.ToBitmap(),
				Size     = new Size(32, 32),
				Location = new Point(12, 12),
				SizeMode = PictureBoxSizeMode.StretchImage
			};
			Controls.Add(pbIcon);

			string warningText = m_bCertChanged
				? "The TLS certificate for the following host has changed since you " +
				  "last accepted it.  This could indicate a security issue."
				: "The TLS certificate for the following host could not be verified.";

			Label lblWarning = new Label
			{
				Text     = warningText,
				Location = new Point(52, 12),
				Size     = new Size(444, 40),
				AutoSize = false
			};
			Controls.Add(lblWarning);

			// ── Certificate details ─────────────────────────────────── //
			X509Certificate2 cert2 = m_cert as X509Certificate2 ??
				new X509Certificate2(m_cert);

			string thumbprint = CertificateExceptionStore.GetSha256Thumbprint(m_cert);
			AcceptedThumbprint = thumbprint;

			string details =
				$"Host:          {m_strHost}\r\n" +
				$"Subject:       {cert2.Subject}\r\n" +
				$"Issuer:        {cert2.Issuer}\r\n" +
				$"Valid from:    {cert2.NotBefore:yyyy-MM-dd}\r\n" +
				$"Valid until:   {cert2.NotAfter:yyyy-MM-dd}\r\n" +
				$"SHA-256:       {thumbprint}\r\n" +
				$"Policy errors: {m_errors}";

			TextBox txtDetails = new TextBox
			{
				Text        = details,
				Location    = new Point(12, 60),
				Size        = new Size(484, 160),
				Multiline   = true,
				ReadOnly    = true,
				ScrollBars  = ScrollBars.Vertical,
				Font        = new Font("Courier New", 8.25f),
				BackColor   = SystemColors.Control
			};
			Controls.Add(txtDetails);

			// ── Prompt label ────────────────────────────────────────── //
			Label lblPrompt = new Label
			{
				Text     = "Do you want to accept this certificate for this host?",
				Location = new Point(12, 228),
				Size     = new Size(484, 20),
				AutoSize = false
			};
			Controls.Add(lblPrompt);

			// ── Buttons ─────────────────────────────────────────────── //
			Button btnAccept = new Button
			{
				Text        = "&Accept (This Session Only)",
				DialogResult = DialogResult.Yes,
				Location    = new Point(12, 260),
				Size        = new Size(190, 30)
			};
			Controls.Add(btnAccept);

			Button btnAcceptAlways = new Button
			{
				Text         = "Accept &Always",
				DialogResult = DialogResult.OK,
				Location     = new Point(208, 260),
				Size         = new Size(140, 30)
			};
			Controls.Add(btnAcceptAlways);

			Button btnReject = new Button
			{
				Text         = "&Reject",
				DialogResult = DialogResult.Cancel,
				Location     = new Point(354, 260),
				Size         = new Size(80, 30)
			};
			Controls.Add(btnReject);

			AcceptButton = btnAcceptAlways;
			CancelButton = btnReject;
		}
	}
}
