using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Utility.Types;
using ECS;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     The realm string Pulse announces peers under and filters incoming messages by. The server
    ///     partitions visibility by exact string match, so this value is the only thing separating two
    ///     sessions that share a Pulse instance.
    ///     Normally it follows <see cref="IRealmData.RealmName" /> live, because a realm change — teleporting
    ///     between Genesis and a world — has to be visible to the very next message the bus sends or filters.
    ///     Local scene development has no realm of its own, so each dev process instead derives one from the
    ///     entity id its dev server serves, which is what keeps concurrent previews from seeing each other.
    ///     Nothing is exchanged: every party — this client, <c>sdk-commands</c>, other explorers — derives the
    ///     identical string from the same entity id, so isolation needs no paired endpoint or handshake. Two
    ///     implementations that derive even slightly different strings do not error; their peers simply never
    ///     see each other. The contract is written down once in js-sdk-toolchain's
    ///     <c>docs/lsd-identity-and-pulse-realm.md</c>; keep this in sync with it.
    /// </summary>
    public class PulseRealm
    {
        /// <summary>
        ///     Pulse's <c>FieldValidatorOptions.MaxRealmLength</c>. A longer realm is rejected server-side.
        /// </summary>
        internal const int MAX_REALM_LENGTH = 255;

        private const string PREFIX = "lsd:";
        private const string HASHED_PREFIX = "lsd:sha256:";
        private const string HEX_DIGITS = "0123456789abcdef";

        private readonly IRealmData realmData;

        /// <summary>
        ///     Null outside local scene development — its presence is what selects the derived realm over
        ///     the one <see cref="realmData" /> reports.
        /// </summary>
        private readonly ILocalSceneEntityIdSource? localSceneEntityIdSource;

        private string localSceneRealm = string.Empty;

        /// <summary>
        ///     Empty while a local scene development realm is unresolved — callers must not connect to Pulse
        ///     in that state, since an empty realm violates the server contract.
        /// </summary>
        public string Value => localSceneEntityIdSource == null ? realmData.RealmName : localSceneRealm;

        public PulseRealm(IRealmData realmData, ILocalSceneEntityIdSource? localSceneEntityIdSource = null)
        {
            this.realmData = realmData;
            this.localSceneEntityIdSource = localSceneEntityIdSource;
        }

        /// <summary>
        ///     Resolves the local scene development realm if it is not known yet; a no-op everywhere else,
        ///     and on every call after the first. Called once before connecting, because the realm ships in
        ///     the very first message — the handshake's initial state.
        ///     Never throws: an unresolvable realm leaves <see cref="Value" /> empty rather than failing the
        ///     log-in flow this runs inside.
        /// </summary>
        public async UniTask EnsureResolvedAsync(CancellationToken ct)
        {
            if (localSceneEntityIdSource == null || localSceneRealm.Length > 0)
                return;

            Result<LocalSceneEntity> entity;

            try { entity = await localSceneEntityIdSource.EntityAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.MULTIPLAYER);
                return;
            }

            if (!entity.Success)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Could not resolve the local scene development Pulse realm: {entity.ErrorMessage}");
                return;
            }

            WarnIfOutsideGenesisBounds(entity.Value.BaseParcel);

            localSceneRealm = RealmKeyFor(entity.Value.Id);
            ReportHub.Log(ReportCategory.MULTIPLAYER, $"Local scene development Pulse realm resolved to '{localSceneRealm}'");
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
