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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General assembly properties
[assembly: AssemblyTitle("KeePassLib")]
[assembly: AssemblyDescription("KeePass Password Management Library")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Dominik Reichl")]
[assembly: AssemblyProduct("KeePassLib")]
[assembly: AssemblyCopyright("Copyright © 2003-2026 Dominik Reichl")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// COM settings
[assembly: ComVisible(false)]

// Assembly GUID
[assembly: Guid("395f6eec-a1e0-4438-aa82-b75099348134")]

// Assembly version information
[assembly: AssemblyVersion("2.61.1.0")]
[assembly: AssemblyFileVersion("2.61.1.0")]

// Allow KeePass.exe to access internal members (replaces legacy linked-file approach)
[assembly: InternalsVisibleTo("KeePass, PublicKey=00240000048000009400000006020000002400005253413100040000010001001f618048344c3cd2c878889433979fea90e4f5615cf89dda25a29e15ba787be8106c14667b12c7a7c2ca4c2d9cf017e1c2c63fe60053501780bb6d4526a2cb196a23e608810ac1ae3c779d4b32a3622d13483939653c719b6da9dbad125c438983ee3e4b1d5ca89e6ac3be6345781977db0e56dbe5795064078f3bf5df5e79b8")]

// Allow the cross-platform test project to access internal members.
// The test assembly is not strongly named so no PublicKey is required here;
// .NET's runtime does not enforce strong-name verification for friend assemblies.
[assembly: InternalsVisibleTo("KeePass.Tests.Platform")]
