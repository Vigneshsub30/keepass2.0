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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeePassLib.Utility
{
    /// <summary>
    /// Static logging bridge for <c>KeePassLib</c> classes.
    ///
    /// KeePassLib was written before DI existed.  Changing every constructor to
    /// accept <c>ILogger&lt;T&gt;</c> would break the existing public API.  Instead, we
    /// provide this static facade that any class in the assembly can use directly:
    ///
    /// <code>
    ///   KeePassLibLog.Logger&lt;FileTransactionEx&gt;().LogError(ex, "Commit failed");
    /// </code>
    ///
    /// The default factory is <see cref="NullLoggerFactory.Instance"/> so all calls
    /// are no-ops unless <see cref="Configure"/> is called by the host application.
    ///
    /// Thread-safe: the factory reference is read atomically; swapping at runtime
    /// after startup is not expected and should not be done in production.
    /// </summary>
    public static class KeePassLibLog
    {
        private static ILoggerFactory _factory = NullLoggerFactory.Instance;

        /// <summary>
        /// Replaces the active logger factory.
        ///
        /// Call this once during application startup, before any KeePassLib
        /// operations are performed.  Subsequent calls replace the factory for
        /// all future <see cref="Logger{T}"/> calls; already-created loggers are
        /// NOT updated.
        ///
        /// Passing <c>null</c> resets to <see cref="NullLoggerFactory.Instance"/>.
        /// </summary>
        public static void Configure(ILoggerFactory factory)
        {
            _factory = factory ?? NullLoggerFactory.Instance;
        }

        /// <summary>
        /// Returns an <see cref="ILogger{T}"/> for the specified category type.
        ///
        /// The returned logger is valid only for the current factory snapshot;
        /// callers should not cache the result across factory replacements.
        /// </summary>
        public static ILogger<T> Logger<T>() => _factory.CreateLogger<T>();

        /// <summary>
        /// Returns an <see cref="ILogger"/> for the specified category name.
        /// </summary>
        public static ILogger Logger(string categoryName) =>
            _factory.CreateLogger(categoryName);
    }
}
