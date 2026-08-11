using System;
using System.Collections.Generic;
using System.IO;

namespace KeePass.Core.Services
{
	/// <summary>
	/// Parses and holds the command-line arguments relevant to KeePass startup.
	/// </summary>
	public sealed class CommandLineArgs
	{
		/// <summary>
		/// Path to a .kdbx file to open on startup, or <see langword="null"/>
		/// if none was specified.
		/// </summary>
		public string? FilePath { get; }

		/// <summary>All unrecognized extra arguments.</summary>
		public IReadOnlyList<string> ExtraArgs { get; }

		private CommandLineArgs(string? filePath, IReadOnlyList<string> extra)
		{
			FilePath  = filePath;
			ExtraArgs = extra;
		}

		/// <summary>
		/// Parse <paramref name="args"/> and extract a file path (any argument
		/// that is a path to an existing .kdbx file or ends with ".kdbx").
		/// </summary>
		public static CommandLineArgs Parse(string[] args)
		{
			if (args == null) throw new ArgumentNullException(nameof(args));

			string? filePath = null;
			var extra = new List<string>();

			foreach (string arg in args)
			{
				if (string.IsNullOrWhiteSpace(arg)) continue;

				if (IsKdbxPath(arg))
				{
					// Last .kdbx argument wins (matches typical OS behavior).
					filePath = arg;
				}
				else
				{
					extra.Add(arg);
				}
			}

			return new CommandLineArgs(filePath, extra.AsReadOnly());
		}

		/// <summary>
		/// Returns <see langword="true"/> when <paramref name="arg"/> looks
		/// like a .kdbx file path.
		/// </summary>
		public static bool IsKdbxPath(string arg)
		{
			if (string.IsNullOrEmpty(arg)) return false;
			return string.Equals(
				Path.GetExtension(arg), ".kdbx",
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
