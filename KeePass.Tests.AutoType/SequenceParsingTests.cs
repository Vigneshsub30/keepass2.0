#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

namespace KeePass.Tests.AutoType
{
	/// <summary>
	/// Tests for auto-type sequence token parsing.
	/// The parsing logic (tokenize brace-delimited directives from literal text)
	/// is tested via a local stub that mirrors the SendInputEx.Parse contract
	/// without Windows P/Invoke dependencies.
	/// </summary>
	public sealed class SequenceParsingTests
	{
		// ── Token model ────────────────────────────────────────────────── //

		private enum TokenKind { Text, Key, Modifier, Delay, Repeat }

		private sealed class SequenceToken
		{
			public TokenKind Kind  { get; }
			public string    Value { get; }
			public int       Count { get; } // for repeat

			public SequenceToken(TokenKind kind, string value, int count = 1)
			{
				Kind  = kind;
				Value = value;
				Count = count;
			}

			public override string ToString() => $"{Kind}:{Value}×{Count}";
		}

		// ── Stub parser ────────────────────────────────────────────────── //

		/// <summary>
		/// Minimal stub that tokenises an auto-type sequence string into
		/// plain-text segments and brace-delimited key tokens.
		/// Mirrors the logical contract of SendInputEx.Parse.
		/// </summary>
		private static List<SequenceToken> ParseSequence(string seq)
		{
			var tokens = new List<SequenceToken>();
			if(string.IsNullOrEmpty(seq)) return tokens;

			int i = 0;
			var text = new StringBuilder();

			while(i < seq.Length)
			{
				char c = seq[i];
				if(c == '{')
				{
					if(text.Length > 0)
					{
						tokens.Add(new SequenceToken(TokenKind.Text, text.ToString()));
						text.Clear();
					}

					int end = seq.IndexOf('}', i + 1);
					if(end < 0)
					{
						// Unclosed brace — treat rest as text.
						text.Append(seq, i, seq.Length - i);
						break;
					}

					string inner = seq.Substring(i + 1, end - i - 1).Trim();
					i = end + 1;

					if(inner.Length == 0) continue;

					// {KEY n} — repeat form
					int spaceIdx = inner.LastIndexOf(' ');
					if(spaceIdx > 0)
					{
						string maybeKey = inner.Substring(0, spaceIdx).Trim();
						string maybeCount = inner.Substring(spaceIdx + 1).Trim();
						if(int.TryParse(maybeCount, out int repeat) && repeat > 0)
						{
							tokens.Add(new SequenceToken(TokenKind.Key, maybeKey, repeat));
							continue;
						}
					}

					// {DELAY n}
					if(inner.StartsWith("DELAY ", StringComparison.OrdinalIgnoreCase))
					{
						tokens.Add(new SequenceToken(TokenKind.Delay, inner));
						continue;
					}

					// {CTRL+KEY} modifier form
					if(inner.Contains('+'))
					{
						tokens.Add(new SequenceToken(TokenKind.Modifier, inner));
						continue;
					}

					tokens.Add(new SequenceToken(TokenKind.Key, inner));
				}
				else
				{
					text.Append(c);
					i++;
				}
			}

			if(text.Length > 0)
				tokens.Add(new SequenceToken(TokenKind.Text, text.ToString()));

			return tokens;
		}

		// ── Tests ──────────────────────────────────────────────────────── //

		[Fact]
		public void Parse_EmptySequence_ReturnsEmpty()
			=> Assert.Empty(ParseSequence(string.Empty));

		[Fact]
		public void Parse_NullSequence_ReturnsEmpty()
			=> Assert.Empty(ParseSequence(null!));

		[Fact]
		public void Parse_PlainText_ReturnsSingleTextToken()
		{
			var tokens = ParseSequence("hello");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Text, t.Kind);
			Assert.Equal("hello", t.Value);
		}

		[Fact]
		public void Parse_EnterKey_ReturnsSingleKeyToken()
		{
			var tokens = ParseSequence("{ENTER}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Key, t.Kind);
			Assert.Equal("ENTER", t.Value);
		}

		[Fact]
		public void Parse_TabKey_ReturnsSingleKeyToken()
		{
			var tokens = ParseSequence("{TAB}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Key, t.Kind);
			Assert.Equal("TAB", t.Value);
		}

		[Fact]
		public void Parse_BackspaceKey_ReturnsSingleKeyToken()
		{
			var tokens = ParseSequence("{BACKSPACE}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Key, t.Kind);
		}

		[Fact]
		public void Parse_TextThenKey_ReturnsTwoTokens()
		{
			var tokens = ParseSequence("user{TAB}");
			Assert.Equal(2, tokens.Count);
			Assert.Equal(TokenKind.Text, tokens[0].Kind);
			Assert.Equal(TokenKind.Key,  tokens[1].Kind);
		}

		[Fact]
		public void Parse_TypicalLoginSequence_ParsesCorrectly()
		{
			var tokens = ParseSequence("{USERNAME}{TAB}{PASSWORD}{ENTER}");
			Assert.Equal(4, tokens.Count);
			Assert.All(tokens, t => Assert.Equal(TokenKind.Key, t.Kind));
		}

		[Fact]
		public void Parse_RepeatKey_SetsCount()
		{
			var tokens = ParseSequence("{LEFT 5}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Key, t.Kind);
			Assert.Equal("LEFT", t.Value);
			Assert.Equal(5, t.Count);
		}

		[Fact]
		public void Parse_DelayDirective_ProducesDelayToken()
		{
			var tokens = ParseSequence("{DELAY 100}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Delay, t.Kind);
		}

		[Fact]
		public void Parse_ModifierCombination_ProducesModifierToken()
		{
			var tokens = ParseSequence("{CTRL+A}");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Modifier, t.Kind);
		}

		[Fact]
		public void Parse_MixedSequence_AllTokensPresent()
		{
			// Text + delay + key + modifier
			var tokens = ParseSequence("abc{DELAY 50}{ENTER}{CTRL+A}");
			Assert.Equal(4, tokens.Count);
			Assert.Equal(TokenKind.Text,     tokens[0].Kind);
			Assert.Equal(TokenKind.Delay,    tokens[1].Kind);
			Assert.Equal(TokenKind.Key,      tokens[2].Kind);
			Assert.Equal(TokenKind.Modifier, tokens[3].Kind);
		}

		[Fact]
		public void Parse_UnclosedBrace_TreatsRestAsText()
		{
			var tokens = ParseSequence("hello{UNCLOSED");
			Assert.Equal(2, tokens.Count);
			Assert.Equal(TokenKind.Text, tokens[0].Kind);
			Assert.Equal("hello", tokens[0].Value);
			Assert.Equal(TokenKind.Text, tokens[1].Kind);
		}

		[Fact]
		public void Parse_EmptyBraces_Skipped()
		{
			var tokens = ParseSequence("{}text");
			var t = Assert.Single(tokens);
			Assert.Equal(TokenKind.Text, t.Kind);
			Assert.Equal("text", t.Value);
		}

		[Fact]
		public void Parse_MultipleRepeats_CorrectCount()
		{
			var tokens = ParseSequence("{DEL 10}");
			var t = Assert.Single(tokens);
			Assert.Equal(10, t.Count);
		}

		[Fact]
		public void Parse_UnicodeText_PreservedInToken()
		{
			string text = "Ünïcödé";
			var tokens = ParseSequence(text);
			var t = Assert.Single(tokens);
			Assert.Equal(text, t.Value);
		}
	}
}
