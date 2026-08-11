using System;
using KeePass.Core.Platform;

namespace KeePass.Core.Infrastructure
{
    /// <summary>
    /// Detects and redacts sensitive vault content (passwords, protected strings)
    /// that must not appear in log output.
    ///
    /// Usage: call <see cref="Redact"/> on any object before embedding it as a
    /// structured log parameter.  For primitive values (int, bool, etc.) the
    /// object is returned unchanged.  For <c>string</c>s, the content is
    /// always treated as potentially sensitive and redacted.
    ///
    /// Design note: this is intentionally conservative — it is better to over-
    /// redact than to leak secrets into log files.
    /// </summary>
    public static class VaultContentRedactionPolicy
    {
        private const string RedactedMarker = "[REDACTED]";

        /// <summary>
        /// Returns a safe log representation of <paramref name="value"/>.
        ///
        /// <list type="bullet">
        ///   <item><c>null</c> → <c>null</c></item>
        ///   <item><c>string</c> → <c>[REDACTED]</c> (always; strings may be passwords)</item>
        ///   <item>Numeric / bool primitives → returned as-is</item>
        ///   <item>All other reference types → <c>[REDACTED]</c></item>
        /// </list>
        /// </summary>
        public static object Redact(object value)
        {
            if (value == null) return null;

            // Numeric and boolean primitives are safe to log.
            if (value is int || value is long || value is uint || value is ulong ||
                value is short || value is ushort || value is byte || value is sbyte ||
                value is float || value is double || value is decimal || value is bool)
            {
                return value;
            }

            // All other types — including string, char, and any object that
            // could carry vault content — are redacted.
            return RedactedMarker;
        }

        /// <summary>
        /// Returns a safe representation of a type name, which is always safe.
        /// </summary>
        public static string SafeTypeName(object value) =>
            value?.GetType().Name ?? "null";

        /// <summary>
        /// Returns a safe representation of an <see cref="Exception"/>:
        /// only the type name and message (no stack trace in production).
        /// </summary>
        public static string SafeException(Exception ex) =>
            ex == null ? string.Empty
                : string.Format("{0}: {1}", ex.GetType().Name, ex.Message);
    }
}
