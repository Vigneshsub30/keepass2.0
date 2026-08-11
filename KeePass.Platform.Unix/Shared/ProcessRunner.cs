using System;
using System.Diagnostics;
using System.Text;

namespace KeePass.Platform.Unix.Shared
{
    /// <summary>
    /// Lightweight helper for running external CLI tools and capturing their
    /// output on macOS and Linux.
    ///
    /// Thread-safe: each call spawns an independent <see cref="Process"/>.
    /// </summary>
    internal static class ProcessRunner
    {
        /// <summary>
        /// Runs an external process and returns its stdout trimmed, or
        /// <c>null</c> if the process fails, times out, or is not found.
        /// </summary>
        /// <param name="executable">Name or full path of the executable.</param>
        /// <param name="arguments">Argument string passed to the process.</param>
        /// <param name="stdinData">
        /// Optional data to write to stdin before reading stdout.
        /// Pass <c>null</c> for no stdin input.
        /// </param>
        /// <param name="timeoutMs">
        /// Maximum wait time in milliseconds. Defaults to 5 000 ms.
        /// </param>
        internal static string Run(string executable, string arguments,
            string stdinData = null, int timeoutMs = 5000)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName               = executable,
                    Arguments              = arguments ?? string.Empty,
                    RedirectStandardInput  = (stdinData != null),
                    RedirectStandardOutput = true,
                    RedirectStandardError  = false,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return null;

                    if (stdinData != null)
                    {
                        p.StandardInput.Write(stdinData);
                        p.StandardInput.Flush();
                        p.StandardInput.Close();
                    }

                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { /* best-effort */ }
                        return null;
                    }

                    string output = p.StandardOutput.ReadToEnd();
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Runs an external process for a side-effect only (writes to stdin,
        /// ignores stdout/stderr). Returns <c>true</c> on exit code 0.
        /// </summary>
        internal static bool RunSilent(string executable, string arguments,
            string stdinData = null, int timeoutMs = 5000)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName               = executable,
                    Arguments              = arguments ?? string.Empty,
                    RedirectStandardInput  = (stdinData != null),
                    RedirectStandardOutput = false,
                    RedirectStandardError  = false,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;

                    if (stdinData != null)
                    {
                        p.StandardInput.Write(stdinData);
                        p.StandardInput.Flush();
                        p.StandardInput.Close();
                    }

                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { /* best-effort */ }
                        return false;
                    }

                    return p.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
