using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Utility.Types;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Isolates each local development process inside the shared Pulse server by deriving the realm
    ///     from the entity id the dev server serves for the previewed project.
    ///     Nothing is exchanged: every party — this client, <c>sdk-commands</c>, other explorers — derives
    ///     the identical string from the same entity id, which is what makes isolation work without any
    ///     paired endpoint or handshake. Two implementations that derive even slightly different strings
    ///     do not error; their peers simply never see each other.
    ///     The contract is written down once in js-sdk-toolchain's
    ///     <c>docs/lsd-identity-and-pulse-realm.md</c>; keep this in sync with it.
    /// </summary>
    public class LocalSceneDevelopmentPulseRealm : IPulseRealm
    {
        /// <summary>
        ///     Pulse's <c>FieldValidatorOptions.MaxRealmLength</c>. A longer realm is rejected server-side.
        /// </summary>
        internal const int MAX_REALM_LENGTH = 255;

        private const string PREFIX = "lsd:";
        private const string HASHED_PREFIX = "lsd:sha256:";
        private const string HEX_DIGITS = "0123456789abcdef";

        private readonly ILocalSceneEntityIdSource entityIdSource;

        private string resolved = string.Empty;

        public string Value => resolved;

        public LocalSceneDevelopmentPulseRealm(ILocalSceneEntityIdSource entityIdSource)
        {
            this.entityIdSource = entityIdSource;
        }

        public async UniTask EnsureResolvedAsync(CancellationToken ct)
        {
            if (resolved.Length > 0)
                return;

            Result<LocalSceneEntity> entity;

            try { entity = await entityIdSource.EntityAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                // Leave the realm unresolved rather than failing the log-in flow this runs inside.
                ReportHub.LogException(e, ReportCategory.MULTIPLAYER);
                return;
            }

            if (!entity.Success)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Could not resolve the local scene development Pulse realm: {entity.ErrorMessage}");
                return;
            }

            WarnIfOutsideGenesisBounds(entity.Value.BaseParcel);

            resolved = RealmKeyFor(entity.Value.Id);
            ReportHub.Log(ReportCategory.MULTIPLAYER, $"Local scene development Pulse realm resolved to '{resolved}'");
        }

        /// <summary>
        ///     <c>"lsd:" + previewSceneId</c>, collapsing to <c>"lsd:sha256:" + SHA256(previewSceneId)</c>
        ///     in lowercase hex once the raw form would exceed <see cref="MAX_REALM_LENGTH" />.
        ///     Hashed rather than truncated on purpose: every party has to land on the same string
        ///     unprompted, and the overflow form is a fixed 75 characters, so it always fits.
        /// </summary>
        internal static string RealmKeyFor(string previewSceneId)
        {
            string realmKey = PREFIX + previewSceneId;

            if (realmKey.Length <= MAX_REALM_LENGTH)
                return realmKey;

            using var sha256 = SHA256.Create();
            return HASHED_PREFIX + ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(previewSceneId)));
        }

        /// <summary>
        ///     Lowercase hex, spelled out rather than culture-dependent formatting, so it is byte-identical
        ///     to the other implementations of the contract.
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];

            for (var i = 0; i < bytes.Length; i++)
            {
                characters[i * 2] = HEX_DIGITS[bytes[i] >> 4];
                characters[(i * 2) + 1] = HEX_DIGITS[bytes[i] & 0xF];
            }

            return new string(characters);
        }

        private static void WarnIfOutsideGenesisBounds(Vector2Int baseParcel)
        {
            bool insideBounds = baseParcel.x >= GenesisCityData.MIN_PARCEL.x && baseParcel.x <= GenesisCityData.MAX_PARCEL.x
                                && baseParcel.y >= GenesisCityData.MIN_PARCEL.y && baseParcel.y <= GenesisCityData.MAX_PARCEL.y;

            if (insideBounds)
                return;

            ReportHub.LogWarning(ReportCategory.MULTIPLAYER,
                $"Local scene base parcel {baseParcel.x},{baseParcel.y} lies outside Genesis City bounds "
                + $"({GenesisCityData.MIN_PARCEL.x},{GenesisCityData.MIN_PARCEL.y} to {GenesisCityData.MAX_PARCEL.x},{GenesisCityData.MAX_PARCEL.y}). "
                + "Pulse rejects parcel indices outside them and disconnects the peer, so player state will not sync. "
                + "Move the scene inside Genesis City bounds, or pass --pulse false to stay on LiveKit only.");
        }
    }
}
