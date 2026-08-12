using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeePass.Core.Browser
{
	/// <summary>
	/// Wire-format envelope for KeePassXC-Browser protocol messages.
	/// Encrypted requests carry action+message+nonce+clientID; the sole
	/// unencrypted request is <c>change-public-keys</c>.
	/// </summary>
	public sealed class BrowserRequestEnvelope
	{
		[JsonPropertyName("action")]
		public string Action { get; set; }

		[JsonPropertyName("message")]
		public string Message { get; set; }

		[JsonPropertyName("nonce")]
		public string Nonce { get; set; }

		[JsonPropertyName("clientID")]
		public string ClientID { get; set; }

		[JsonPropertyName("publicKey")]
		public string PublicKey { get; set; }

		[JsonPropertyName("requestID")]
		public string RequestID { get; set; }
	}

	/// <summary>
	/// Decrypted inner payload of an encrypted request.
	/// Additional fields are action-specific and extracted via raw JSON.
	/// </summary>
	public sealed class DecryptedRequest
	{
		[JsonPropertyName("action")]
		public string Action { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; }

		[JsonPropertyName("submitUrl")]
		public string SubmitUrl { get; set; }

		[JsonPropertyName("httpAuth")]
		public string HttpAuth { get; set; }

		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("key")]
		public string Key { get; set; }

		[JsonPropertyName("idKey")]
		public string IdKey { get; set; }

		[JsonPropertyName("login")]
		public string Login { get; set; }

		[JsonPropertyName("password")]
		public string Password { get; set; }

		[JsonPropertyName("group")]
		public string Group { get; set; }

		[JsonPropertyName("groupUuid")]
		public string GroupUuid { get; set; }

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; }

		[JsonPropertyName("groupName")]
		public string GroupName { get; set; }

		[JsonPropertyName("keys")]
		public List<AssociationKeyEntry> Keys { get; set; }
	}

	/// <summary>
	/// A single (id, key) pair sent in <c>get-logins</c> and <c>test-associate</c>.
	/// </summary>
	public sealed class AssociationKeyEntry
	{
		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("key")]
		public string Key { get; set; }
	}

	/// <summary>
	/// Login entry returned in <c>get-logins</c> responses.
	/// </summary>
	public sealed class LoginEntryDto
	{
		[JsonPropertyName("login")]
		public string Login { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("password")]
		public string Password { get; set; }

		[JsonPropertyName("group")]
		public string Group { get; set; }

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; }

		[JsonPropertyName("expired")]
		public string Expired { get; set; }

		[JsonPropertyName("totp")]
		public string Totp { get; set; }
	}

	/// <summary>
	/// Group tree node returned in <c>get-database-groups</c> responses.
	/// </summary>
	public sealed class GroupTreeDto
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; }

		[JsonPropertyName("children")]
		public List<GroupTreeDto> Children { get; set; } = new List<GroupTreeDto>();
	}
}
