using System;
using System.Runtime.Serialization;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// Thrown by <see cref="ImportValidationPipeline"/> when an input stream
	/// violates one of the configured ceilings.
	/// </summary>
	[Serializable]
	public sealed class ImportValidationException : Exception
	{
		/// <summary>Name of the ceiling that was exceeded (e.g. "MaxFileSize").</summary>
		public string CeilingName { get; }

		/// <summary>The configured limit that was exceeded.</summary>
		public long ConfiguredLimit { get; }

		/// <summary>
		/// The observed value that triggered the rejection (bytes read,
		/// nesting depth, etc.).  -1 when not applicable.
		/// </summary>
		public long ObservedValue { get; }

		/// <param name="ceilingName">Name of the ceiling property that was exceeded.</param>
		/// <param name="configuredLimit">The configured ceiling value.</param>
		/// <param name="observedValue">The value that exceeded the limit, or -1.</param>
		public ImportValidationException(string ceilingName, long configuredLimit,
			long observedValue)
			: base(BuildMessage(ceilingName, configuredLimit, observedValue))
		{
			CeilingName     = ceilingName;
			ConfiguredLimit = configuredLimit;
			ObservedValue   = observedValue;
		}

		private static string BuildMessage(string name, long limit, long observed)
		{
			if(observed >= 0)
				return $"Import rejected: {name} ceiling ({limit:N0}) exceeded " +
					$"(observed {observed:N0}).";
			return $"Import rejected: {name} ceiling ({limit:N0}) exceeded.";
		}

		[Obsolete("For serialization only.", error: false)]
		private ImportValidationException(SerializationInfo info,
			StreamingContext ctx) : base(info, ctx)
		{
			CeilingName     = info.GetString(nameof(CeilingName)) ?? string.Empty;
			ConfiguredLimit = info.GetInt64(nameof(ConfiguredLimit));
			ObservedValue   = info.GetInt64(nameof(ObservedValue));
		}
	}
}
