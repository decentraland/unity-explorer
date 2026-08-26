using System;
using System.Collections.Generic;
using System.Globalization;
using Nethereum.Signer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// A Decentraland auth identity pasted from the Builder web app (browser localStorage while
    /// logged into builder.decentraland.org). Contains the wallet's auth chain plus an ephemeral
    /// private key that signs individual requests — the same recipe the explorer uses
    /// (see unity-explorer: DecentralandIdentity.Sign, RequestEnvelope.SignRequest,
    /// WebRequestSignInfo.NewFromRaw).
    ///
    /// SECURITY: the ephemeral private key is a short-lived credential. It is persisted ONLY in
    /// EditorPrefs (per-user, outside the repo) and must never be logged or written to project files.
    /// </summary>
    public class BuilderIdentity
    {
        private const string EDITOR_PREFS_KEY = "OutfitStudio.BuilderIdentity";

        public string EphemeralPrivateKey;
        public DateTime Expiration;
        public List<AuthLink> AuthChain = new();

        [Serializable]
        public class AuthLink
        {
            public string type;
            public string payload;
            public string signature;
        }

        public bool IsExpired => DateTime.UtcNow >= Expiration;

        /// <summary>The wallet address (the auth chain's SIGNER payload).</summary>
        public string WalletAddress =>
            AuthChain.Find(l => l.type == "SIGNER")?.payload ?? "<unknown>";

        /// <summary>
        /// Parses a pasted identity. Tolerant of wrappers: accepts the bare @dcl/crypto
        /// AuthIdentity object or any JSON (e.g. a whole localStorage entry) containing one,
        /// including identities nested as stringified JSON.
        /// </summary>
        public static BuilderIdentity Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Empty identity");

            var identityToken = FindIdentityObject(JToken.Parse(json.Trim()), depth: 0);

            if (identityToken == null)
                throw new FormatException(
                    "No identity found in the pasted JSON — expected an object with ephemeralIdentity, expiration and authChain");

            var ephemeral = identityToken["ephemeralIdentity"];
            var privateKey = ephemeral?["privateKey"]?.Value<string>();

            if (string.IsNullOrEmpty(privateKey))
                throw new FormatException("Identity has no ephemeral private key");

            var identity = new BuilderIdentity
            {
                EphemeralPrivateKey = privateKey,
                Expiration = DateTime.Parse(identityToken["expiration"]!.Value<string>()!,
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                AuthChain = identityToken["authChain"]!.ToObject<List<AuthLink>>()
            };

            if (identity.AuthChain == null || identity.AuthChain.Count == 0)
                throw new FormatException("Identity has an empty auth chain");

            return identity;
        }

        private static JObject FindIdentityObject(JToken token, int depth)
        {
            if (depth > 8 || token == null) return null;

            switch (token)
            {
                case JObject obj when obj["ephemeralIdentity"] != null && obj["expiration"] != null &&
                                      obj["authChain"] != null:
                    return obj;

                case JObject obj:
                {
                    foreach (var property in obj.Properties())
                    {
                        var found = FindIdentityObject(property.Value, depth + 1);
                        if (found != null) return found;
                    }

                    break;
                }

                case JArray array:
                {
                    foreach (var element in array)
                    {
                        var found = FindIdentityObject(element, depth + 1);
                        if (found != null) return found;
                    }

                    break;
                }

                case JValue { Type: JTokenType.String } value:
                {
                    // Some storage formats nest the identity as stringified JSON
                    var text = value.Value<string>();
                    if (text != null && text.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            return FindIdentityObject(JToken.Parse(text), depth + 1);
                        }
                        catch
                        {
                            // not JSON — ignore
                        }
                    }

                    break;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- Persistence (EditorPrefs only)

        public void Save()
        {
            var json = JsonConvert.SerializeObject(new
            {
                ephemeralIdentity = new { privateKey = EphemeralPrivateKey },
                expiration = Expiration.ToString("o"),
                authChain = AuthChain
            });

            EditorPrefs.SetString(EDITOR_PREFS_KEY, json);
        }

        public static BuilderIdentity Load()
        {
            var json = EditorPrefs.GetString(EDITOR_PREFS_KEY, null);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return Parse(json);
            }
            catch
            {
                return null;
            }
        }

        public static void Clear() => EditorPrefs.DeleteKey(EDITOR_PREFS_KEY);

        // ---------------------------------------------------------------- Signing

        /// <summary>
        /// Builds the signed-fetch headers for a request, mirroring the explorer exactly:
        /// string-to-sign = "{method}:{path}:{unixMs}:{metadata}" lowercased, signed by the
        /// ephemeral key (Ethereum personal sign) and appended to the stored auth chain as an
        /// ECDSA_SIGNED_ENTITY link. Each link becomes an x-identity-auth-chain-{i} header.
        /// </summary>
        public Dictionary<string, string> SignedHeaders(string method, string path)
        {
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            const string metadata = "{}";

            var stringToSign = $"{method}:{path}:{timestamp}:{metadata}".ToLowerInvariant();
            var signature = new EthereumMessageSigner()
                .EncodeUTF8AndSign(stringToSign, new EthECKey(EphemeralPrivateKey));

            var headers = new Dictionary<string, string>
            {
                ["x-identity-timestamp"] = timestamp.ToString(),
                ["x-identity-metadata"] = metadata
            };

            var i = 0;
            foreach (var link in AuthChain)
            {
                headers[$"x-identity-auth-chain-{i}"] = JsonConvert.SerializeObject(link);
                i++;
            }

            headers[$"x-identity-auth-chain-{i}"] = JsonConvert.SerializeObject(new AuthLink
            {
                type = "ECDSA_SIGNED_ENTITY",
                payload = stringToSign,
                signature = signature
            });

            return headers;
        }
    }
}
