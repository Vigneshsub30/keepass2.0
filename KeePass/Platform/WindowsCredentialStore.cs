/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using KeePass.Core.Platform;

namespace KeePass.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="ICredentialStore"/> backed by the
    /// Windows Credential Manager (<c>Advapi32.dll</c> CredRead/CredWrite/CredDelete).
    ///
    /// Credentials are stored under the <c>CRED_TYPE_GENERIC</c> credential type
    /// and identified by a caller-supplied key string.  The secret bytes are
    /// stored directly in the <c>CredentialBlob</c> field (limit: 512 bytes on
    /// Windows; callers should pre-encrypt using DPAPI if larger blobs are needed).
    ///
    /// Thread-safe: native API calls are individually atomic; no additional
    /// locking is required.
    /// </summary>
    public sealed class WindowsCredentialStore : ICredentialStore
    {
        private const int CredTypeGeneric = 1;

        // CREDENTIAL.Flags bitmask — no special flags needed for generic creds.
        private const int CredFlagsNone = 0;

        // CREDENTIAL.Persist values.
        private const uint CredPersistLocalMachine = 2; // survives user session

        /// <inheritdoc/>
        /// <remarks>Always <c>true</c> on Windows.</remarks>
        public bool IsSupported => true;

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> or <paramref name="secret"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="secret"/> is empty or <paramref name="key"/>
        /// is an empty string.
        /// </exception>
        public void Store(string key, byte[] secret)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (key.Length == 0) throw new ArgumentException("Key must not be empty.", "key");
            if (secret == null) throw new ArgumentNullException("secret");
            if (secret.Length == 0) throw new ArgumentException("Secret must not be empty.", "secret");

            // Encode secret as UTF-16 hex so it fits in CredentialBlob safely.
            // CredentialBlob is a raw byte array but some tools display it as
            // a string; hex encoding avoids encoding ambiguities.
            string hexSecret = BytesToHex(secret);
            byte[] blob = Encoding.Unicode.GetBytes(hexSecret);

            CREDENTIAL cred = new CREDENTIAL
            {
                Flags = CredFlagsNone,
                Type = CredTypeGeneric,
                TargetName = key,
                Comment = "KeePass credential store",
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = Marshal.AllocCoTaskMem(blob.Length),
                Persist = CredPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = key,
            };

            try
            {
                Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);

                if (!CredWrite(ref cred, 0))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        $"CredWrite failed for key '{key}'. Win32 error: {err}.");
                }
            }
            finally
            {
                if (cred.CredentialBlob != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(cred.CredentialBlob);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> is null.
        /// </exception>
        public byte[] Retrieve(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            IntPtr pCred = IntPtr.Zero;
            try
            {
                if (!CredRead(key, CredTypeGeneric, 0, out pCred))
                    return null; // key not found; not an error

                CREDENTIAL cred = Marshal.PtrToStructure<CREDENTIAL>(pCred);
                if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                    return null;

                byte[] blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);

                string hexSecret = Encoding.Unicode.GetString(blob);
                return HexToBytes(hexSecret);
            }
            finally
            {
                if (pCred != IntPtr.Zero) CredFree(pCred);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> is null.
        /// </exception>
        public void Delete(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            // CredDelete returns false if the key does not exist; treat as no-op.
            if (!CredDelete(key, CredTypeGeneric, 0))
            {
                int err = Marshal.GetLastWin32Error();
                const int ErrorNotFound = 1168; // ERROR_NOT_FOUND
                if (err != ErrorNotFound)
                {
                    Debug.Assert(false);
                    throw new InvalidOperationException(
                        $"CredDelete failed for key '{key}'. Win32 error: {err}.");
                }
            }
        }

        // ── Hex encoding helpers ───────────────────────────────────────────

        private static string BytesToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            if (hex.Length % 2 != 0) return null; // corrupted

            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber, null, out result[i]))
                    return null; // corrupted
            }
            return result;
        }

        // ── CREDENTIAL struct (Advapi32 CredRead/CredWrite layout) ─────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL_ATTRIBUTE
        {
            public string Keyword;
            public uint Flags;
            public uint ValueSize;
            public IntPtr Value;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        // ── P/Invoke declarations ──────────────────────────────────────────

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string target, int type, int reservedFlag,
            out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref CREDENTIAL userCredential, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("Advapi32.dll")]
        private static extern void CredFree(IntPtr cred);
    }
}
