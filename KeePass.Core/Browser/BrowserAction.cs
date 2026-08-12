using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using KeePassLib;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;

using KeePass.Core.Services;

namespace KeePass.Core.Browser
{
	/// <summary>
	/// Dispatches decrypted KeePassXC-Browser protocol actions to handler
	/// methods and returns the JSON response string ready for framing.
	/// </summary>
	public sealed class BrowserAction
	{
		private const string ProtocolVersion = "2.7.0";
		private const string CustomDataKeyPrefix = "KPXC_BROWSER_";

		private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		private readonly IDatabaseSessionService _session;

		public BrowserAction(IDatabaseSessionService session)
		{
			_session = session ?? throw new ArgumentNullException(nameof(session));
		}

		/// <summary>
		/// Processes a raw JSON request envelope from the browser and returns
		/// the raw JSON response string.
		/// </summary>
		public string ProcessMessage(string json, BrowserSession session)
		{
			BrowserRequestEnvelope envelope;
			try
			{
				envelope = JsonSerializer.Deserialize<BrowserRequestEnvelope>(json);
			}
			catch (JsonException)
			{
				return BuildErrorResponse("unknown", "Cannot parse request");
			}

			if (envelope == null || string.IsNullOrEmpty(envelope.Action))
				return BuildErrorResponse("unknown", "Missing action");

			if (envelope.Action == "change-public-keys")
				return HandleChangePublicKeys(envelope, session);

			if (!session.KeyExchangeDone)
				return BuildErrorResponse(envelope.Action, "Key exchange not completed");

			string decryptedJson = session.Decrypt(envelope.Message, envelope.Nonce);
			if (decryptedJson == null)
				return BuildErrorResponse(envelope.Action, "Cannot decrypt message");

			DecryptedRequest request;
			try
			{
				request = JsonSerializer.Deserialize<DecryptedRequest>(decryptedJson);
			}
			catch (JsonException)
			{
				return BuildErrorResponse(envelope.Action, "Invalid decrypted payload");
			}

			string responseNonce = NaClCrypto.IncrementNonce(envelope.Nonce);

			Dictionary<string, object> responseData;
			switch (envelope.Action)
			{
				case "associate":
					responseData = HandleAssociate(request, session);
					break;
				case "test-associate":
					responseData = HandleTestAssociate(request, session);
					break;
				case "get-databasehash":
					responseData = HandleGetDatabaseHash();
					break;
				case "get-logins":
					responseData = HandleGetLogins(request, session);
					break;
				case "set-login":
					responseData = HandleSetLogin(request);
					break;
				case "generate-password":
					responseData = HandleGeneratePassword();
					break;
				case "lock-database":
					responseData = HandleLockDatabase();
					break;
				case "get-database-groups":
					responseData = HandleGetDatabaseGroups();
					break;
				default:
					responseData = new Dictionary<string, object>
					{
						["error"] = "Unknown action",
						["errorCode"] = 1
					};
					break;
			}

			responseData["nonce"] = responseNonce;
			responseData["version"] = ProtocolVersion;

			string responsePlain = JsonSerializer.Serialize(responseData, JsonOpts);
			string encryptedMessage = session.Encrypt(responsePlain, responseNonce);

			var responseEnvelope = new Dictionary<string, object>
			{
				["action"] = envelope.Action,
				["message"] = encryptedMessage,
				["nonce"] = responseNonce
			};

			return JsonSerializer.Serialize(responseEnvelope, JsonOpts);
		}

		private string HandleChangePublicKeys(BrowserRequestEnvelope envelope, BrowserSession session)
		{
			if (string.IsNullOrEmpty(envelope.PublicKey))
				return BuildErrorResponse("change-public-keys", "Missing client public key");

			session.ClientID = envelope.ClientID;
			session.SetClientPublicKey(envelope.PublicKey);

			string responseNonce = NaClCrypto.IncrementNonce(envelope.Nonce);

			var response = new Dictionary<string, object>
			{
				["action"] = "change-public-keys",
				["publicKey"] = session.ServerPublicKeyB64,
				["nonce"] = responseNonce,
				["version"] = ProtocolVersion,
				["success"] = "true"
			};

			return JsonSerializer.Serialize(response, JsonOpts);
		}

		private Dictionary<string, object> HandleAssociate(DecryptedRequest request, BrowserSession session)
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			string idKey = request.IdKey;
			if (string.IsNullOrEmpty(idKey))
				return ErrorPayload("Missing identification key");

			string hash = GetDatabaseHash(db);
			string associationId = "keepass-browser-" +
				Convert.ToBase64String(Guid.NewGuid().ToByteArray())
					.TrimEnd('=').Replace('+', '-').Replace('/', '_');

			session.Associations[associationId] = idKey;

			db.CustomData.Set(CustomDataKeyPrefix + associationId, idKey);
			db.Modified = true;

			return new Dictionary<string, object>
			{
				["hash"] = hash,
				["success"] = "true",
				["id"] = associationId
			};
		}

		private Dictionary<string, object> HandleTestAssociate(DecryptedRequest request, BrowserSession session)
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			string id = request.Id;
			string key = request.Key;

			if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
				return ErrorPayload("Missing id or key");

			string storedKey = db.CustomData.Get(CustomDataKeyPrefix + id);
			if (storedKey == null || storedKey != key)
				return ErrorPayload("Association failed");

			session.Associations[id] = key;

			return new Dictionary<string, object>
			{
				["hash"] = GetDatabaseHash(db),
				["id"] = id,
				["success"] = "true"
			};
		}

		private Dictionary<string, object> HandleGetDatabaseHash()
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			return new Dictionary<string, object>
			{
				["action"] = "hash",
				["hash"] = GetDatabaseHash(db),
				["success"] = "true"
			};
		}

		private Dictionary<string, object> HandleGetLogins(DecryptedRequest request, BrowserSession session)
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			if (string.IsNullOrEmpty(request.Url))
				return ErrorPayload("No URL provided");

			if (!VerifyAssociationKeys(request.Keys, db, session))
				return ErrorPayload("Association not verified");

			Uri requestUri;
			if (!Uri.TryCreate(request.Url, UriKind.Absolute, out requestUri))
				return ErrorPayload("Invalid URL");

			var entries = new List<LoginEntryDto>();
			CollectMatchingEntries(db.RootGroup, requestUri, entries);

			if (entries.Count == 0)
				return ErrorPayload("No logins found");

			return new Dictionary<string, object>
			{
				["count"] = entries.Count.ToString(),
				["entries"] = entries.Select(e => new Dictionary<string, object>
				{
					["login"] = e.Login ?? string.Empty,
					["name"] = e.Name ?? string.Empty,
					["password"] = e.Password ?? string.Empty,
					["group"] = e.Group ?? string.Empty,
					["uuid"] = e.Uuid ?? string.Empty,
					["expired"] = e.Expired ?? string.Empty
				}).ToArray(),
				["hash"] = GetDatabaseHash(db),
				["id"] = request.Id ?? string.Empty,
				["success"] = "true"
			};
		}

		private Dictionary<string, object> HandleSetLogin(DecryptedRequest request)
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			PwGroup targetGroup = db.RootGroup;
			if (!string.IsNullOrEmpty(request.GroupUuid))
			{
				PwUuid groupUuid = ParseUuid(request.GroupUuid);
				if (groupUuid != null)
				{
					PwGroup found = db.RootGroup.FindGroup(groupUuid, true);
					if (found != null) targetGroup = found;
				}
			}

			PwEntry entry = null;
			if (!string.IsNullOrEmpty(request.Uuid))
			{
				PwUuid entryUuid = ParseUuid(request.Uuid);
				if (entryUuid != null)
					entry = db.RootGroup.FindEntry(entryUuid, true);
			}

			if (entry == null)
			{
				entry = new PwEntry(true, true);
				targetGroup.AddEntry(entry, true);
			}

			if (!string.IsNullOrEmpty(request.Login))
				entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, request.Login));
			if (!string.IsNullOrEmpty(request.Password))
				entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, request.Password));
			if (!string.IsNullOrEmpty(request.Url))
				entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, request.Url));

			string title = request.SubmitUrl ?? request.Url ?? "Browser Entry";
			if (Uri.TryCreate(title, UriKind.Absolute, out Uri titleUri))
				title = titleUri.Host;
			entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));

			entry.Touch(true);
			db.Modified = true;

			return new Dictionary<string, object>
			{
				["hash"] = GetDatabaseHash(db),
				["success"] = "true",
				["error"] = string.Empty
			};
		}

		private Dictionary<string, object> HandleGeneratePassword()
		{
			var profile = new PwProfile
			{
				Length = 20,
				CharSet = new PwCharSet(PwCharSet.UpperCase +
					PwCharSet.LowerCase + PwCharSet.Digits +
					PwCharSet.PrintableAsciiSpecial)
			};

			PwgError err = PwGenerator.Generate(out ProtectedString ps, profile, null, null);
			string password = err == PwgError.Success && ps != null
				? ps.ReadString()
				: Guid.NewGuid().ToString("N").Substring(0, 20);

			return new Dictionary<string, object>
			{
				["password"] = password,
				["success"] = "true"
			};
		}

		private Dictionary<string, object> HandleLockDatabase()
		{
			try
			{
				_session.LockWorkspace();
			}
			catch { /* best effort */ }

			return new Dictionary<string, object>
			{
				["action"] = "lock-database",
				["success"] = "true"
			};
		}

		private Dictionary<string, object> HandleGetDatabaseGroups()
		{
			PwDatabase db = _session.GetActiveDatabase();
			if (db == null || !db.IsOpen)
				return ErrorPayload("Database not opened");

			GroupTreeDto rootDto = BuildGroupTree(db.RootGroup);

			return new Dictionary<string, object>
			{
				["defaultGroup"] = db.RootGroup.Name,
				["defaultGroupAlwaysAllow"] = false,
				["groups"] = new object[] { SerializeGroupTree(rootDto) },
				["success"] = "true"
			};
		}

		// ── Helpers ──────────────────────────────────────────────────────────

		private static string GetDatabaseHash(PwDatabase db)
		{
			if (db.RootGroup == null) return string.Empty;
			byte[] uuidBytes = db.RootGroup.Uuid.UuidBytes;
			using (var sha256 = SHA256.Create())
			{
				byte[] hash = sha256.ComputeHash(uuidBytes);
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		private bool VerifyAssociationKeys(
			List<AssociationKeyEntry> keys, PwDatabase db, BrowserSession session)
		{
			if (keys == null || keys.Count == 0) return false;

			foreach (var k in keys)
			{
				if (string.IsNullOrEmpty(k.Id) || string.IsNullOrEmpty(k.Key))
					continue;

				string storedKey = db.CustomData.Get(CustomDataKeyPrefix + k.Id);
				if (storedKey != null && storedKey == k.Key)
				{
					session.Associations[k.Id] = k.Key;
					return true;
				}
			}

			return false;
		}

		private void CollectMatchingEntries(PwGroup group, Uri requestUri, List<LoginEntryDto> results)
		{
			for (uint i = 0; i < group.Entries.UCount; i++)
			{
				PwEntry entry = group.Entries.GetAt(i);
				string entryUrl = entry.Strings.ReadSafe(PwDefs.UrlField);
				if (string.IsNullOrEmpty(entryUrl)) continue;

				if (IsUrlMatch(entryUrl, requestUri))
				{
					bool expired = entry.Expires && entry.ExpiryTime <= DateTime.UtcNow;
					results.Add(new LoginEntryDto
					{
						Login = entry.Strings.ReadSafe(PwDefs.UserNameField),
						Name = entry.Strings.ReadSafe(PwDefs.TitleField),
						Password = entry.Strings.GetSafe(PwDefs.PasswordField).ReadString(),
						Group = entry.ParentGroup?.Name ?? string.Empty,
						Uuid = Convert.ToBase64String(entry.Uuid.UuidBytes),
						Expired = expired ? "true" : null
					});
				}
			}

			for (uint i = 0; i < group.Groups.UCount; i++)
				CollectMatchingEntries(group.Groups.GetAt(i), requestUri, results);
		}

		/// <summary>
		/// Matches by scheme + host (case-insensitive). Path is prefix-matched.
		/// Query and fragment are ignored.
		/// </summary>
		private static bool IsUrlMatch(string entryUrl, Uri requestUri)
		{
			if (!Uri.TryCreate(entryUrl, UriKind.Absolute, out Uri entryUri))
			{
				if (!entryUrl.Contains("://"))
				{
					if (!Uri.TryCreate("https://" + entryUrl, UriKind.Absolute, out entryUri))
						return false;
				}
				else
				{
					return false;
				}
			}

			if (!string.Equals(entryUri.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase))
				return false;

			if (!string.Equals(entryUri.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase))
				return false;

			if (entryUri.Port != requestUri.Port &&
				entryUri.Port != -1 && requestUri.Port != -1)
				return false;

			string entryPath = entryUri.AbsolutePath.TrimEnd('/');
			string requestPath = requestUri.AbsolutePath.TrimEnd('/');

			if (!string.IsNullOrEmpty(entryPath) && entryPath != "/" &&
				!requestPath.StartsWith(entryPath, StringComparison.OrdinalIgnoreCase))
				return false;

			return true;
		}

		private static GroupTreeDto BuildGroupTree(PwGroup group)
		{
			var dto = new GroupTreeDto
			{
				Name = group.Name,
				Uuid = Convert.ToBase64String(group.Uuid.UuidBytes)
			};

			for (uint i = 0; i < group.Groups.UCount; i++)
				dto.Children.Add(BuildGroupTree(group.Groups.GetAt(i)));

			return dto;
		}

		private static Dictionary<string, object> SerializeGroupTree(GroupTreeDto dto)
		{
			var children = new List<Dictionary<string, object>>();
			foreach (var child in dto.Children)
				children.Add(SerializeGroupTree(child));

			return new Dictionary<string, object>
			{
				["name"] = dto.Name,
				["uuid"] = dto.Uuid,
				["children"] = children
			};
		}

		private static PwUuid ParseUuid(string base64OrHex)
		{
			try
			{
				byte[] bytes = Convert.FromBase64String(base64OrHex);
				if (bytes.Length == 16)
					return new PwUuid(bytes);
			}
			catch { /* not valid base64, ignore */ }
			return null;
		}

		private static string BuildErrorResponse(string action, string error)
		{
			var response = new Dictionary<string, object>
			{
				["action"] = action,
				["error"] = error,
				["errorCode"] = 1
			};
			return JsonSerializer.Serialize(response, JsonOpts);
		}

		private static Dictionary<string, object> ErrorPayload(string error)
		{
			return new Dictionary<string, object>
			{
				["error"] = error,
				["errorCode"] = 1
			};
		}
	}
}
