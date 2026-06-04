using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;
using Newtonsoft.Json;
using Utility.Json;
using Utility.Times;

namespace DCL.Backpack.AvatarSection.Outfits.Repository
{
    using OutfitsEntity = EntityDefinitionGeneric<OutfitsMetadata>;

    /// <summary>
    ///     Owns the user's authoritative outfits state and publishes it to the Catalyst network.
    ///     Per-call, naive: each Save/Delete builds a fresh snapshot from <see cref="committed"/>
    ///     plus its own delta, waits the deploy window, publishes, and on success commits the delta
    ///     locally. Serialization across operations is enforced at the UI layer
    ///     (<see cref="DCL.Backpack.OutfitsPresenter"/> disables save/delete buttons while one
    ///     operation is in flight), so this class assumes no concurrent callers.
    /// </summary>
    public class OutfitsRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new ()
            { Converters = new List<JsonConverter> { new ColorJsonConverter() } };

        private const int DEFAULT_DEPLOY_WINDOW_IN_SECONDS = 15;

        private readonly PublishIpfsEntityCommand publishIpfsEntityCommand;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISelfProfile selfProfile;

        private readonly Dictionary<int, OutfitItem> committed = new ();
        private ulong lastDeployTimestampInSeconds;

        public OutfitsRepository(PublishIpfsEntityCommand publishIpfsEntityCommand,
            INftNamesProvider nftNamesProvider,
            ISelfProfile selfProfile)
        {
            this.publishIpfsEntityCommand = publishIpfsEntityCommand;
            this.nftNamesProvider = nftNamesProvider;
            this.selfProfile = selfProfile;
        }

        private static int deployWindowInSeconds
        {
            get
            {
                if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.OUTFITS_DEPLOY_WINDOW,
                        "deploy_window_in_seconds",
                        out OutfitsDeployWindowConfig? config) && config.HasValue && config.Value.DeployWindowInSeconds > 0)
                    return config.Value.DeployWindowInSeconds;

                return DEFAULT_DEPLOY_WINDOW_IN_SECONDS;
            }
        }

        [Serializable]
        private struct OutfitsDeployWindowConfig
        {
            [JsonProperty("deploy_window_in_seconds")] public int DeployWindowInSeconds;
        }

        /// <summary>
        ///     Seeds the authoritative state from a freshly fetched server snapshot.
        ///     Idempotent — safe to call on every panel open.
        /// </summary>
        public void Initialize(IEnumerable<OutfitItem> outfits)
        {
            committed.Clear();
            foreach (var item in outfits)
                committed[item.slot] = item;
        }

        public async UniTask SaveSlotAsync(int slot, OutfitItem item, CancellationToken ct)
        {
            // Cancellation is only honored during the deploy-window wait. Once we get past
            // this line we commit to publishing — the publish itself is non-cancellable
            // (see PublishAsync) so the server-side and local-side state stay in sync.
            await WaitRemainingDeployWindowAsync(ct);

            var snapshot = new Dictionary<int, OutfitItem>(committed) { [slot] = item };
            await PublishAsync(snapshot, ct);

            committed[slot] = item;
            lastDeployTimestampInSeconds = NowSeconds();
        }

        public async UniTask DeleteSlotAsync(int slot, CancellationToken ct)
        {
            if (!committed.ContainsKey(slot))
                return;

            await WaitRemainingDeployWindowAsync(ct);

            var snapshot = new Dictionary<int, OutfitItem>(committed);
            snapshot.Remove(slot);
            await PublishAsync(snapshot, ct);

            committed.Remove(slot);
            lastDeployTimestampInSeconds = NowSeconds();
        }

        private async UniTask WaitRemainingDeployWindowAsync(CancellationToken ct)
        {
            int deployWindow = deployWindowInSeconds;
            ulong elapsed = Math.Clamp(NowSeconds() - lastDeployTimestampInSeconds, 0UL, (ulong)deployWindow);
            double remaining = deployWindow - (double)elapsed;
            if (remaining <= 0) return;

            await UniTask.Delay(TimeSpan.FromSeconds(remaining), cancellationToken: ct);
        }

        private async UniTask PublishAsync(Dictionary<int, OutfitItem> snapshot, CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);
            if (profile == null)
                throw new InvalidOperationException("Cannot publish outfits, self profile is not loaded.");

            if (string.IsNullOrEmpty(profile.UserId))
                throw new InvalidOperationException("Cannot publish outfits for a user with an empty UserId.");

            INftNamesProvider.PaginatedNamesResponse namesForExtraSlots =
                await nftNamesProvider.GetAsync(new Web3Address(profile.UserId), 1, 1, ct);

            var metadata = new OutfitsMetadata
            {
                outfits = snapshot.Values.ToList(),
                namesForExtraSlots = namesForExtraSlots.Names.Count > 0
                    ? new List<string> { namesForExtraSlots.Names[0] }
                    : new List<string>(),
            };

            var outfitsEntity = new OutfitsEntity(string.Empty, metadata)
            {
                version = OutfitsEntity.DEFAULT_VERSION,
                pointers = new[] { $"{profile.UserId}:outfits" },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = IpfsRealmEntityType.Outfits.ToEntityString(),
                content = Array.Empty<ContentDefinition>(),
            };

            // The publish itself is intentionally non-cancellable: once we've decided to send
            // the request, we let it complete. Cancelling the HTTP call mid-flight just creates
            // ambiguity ("did the server receive it?") and produces noisy error logs from
            // PublishIpfsEntityCommand for what is really an expected panel-close scenario.
            // Cancellation during the deploy-window wait above is still honored cleanly.
            await publishIpfsEntityCommand.ExecuteAsync(outfitsEntity, CancellationToken.None, SERIALIZER_SETTINGS);
        }

        private static ulong NowSeconds() =>
            DateTime.UtcNow.UnixTimeAsMilliseconds() / 1000;
    }
}
