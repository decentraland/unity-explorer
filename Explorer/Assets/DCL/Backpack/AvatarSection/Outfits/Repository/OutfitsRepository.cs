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
    /// </summary>
    public class OutfitsRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new ()
            { Converters = new List<JsonConverter> { new ColorJsonConverter() } };

        /// <summary>
        ///     Minimum number of seconds we wait between successive publishes to the
        ///     Catalyst content endpoint. This matches the backend's per-pointer rate limit on
        ///     <c>:outfits</c> entity submissions — publishing inside the cooldown gets rejected.
        ///     Configurable in production via the <c>OUTFITS_DEPLOY_WINDOW</c> feature flag.
        /// </summary>
        private const int DEFAULT_DEPLOY_COOLDOWN_SECONDS = 15;

        private readonly PublishIpfsEntityCommand publishIpfsEntityCommand;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISelfProfile selfProfile;

        private readonly Dictionary<int, OutfitItem> committed = new ();
        private ulong lastPublishTimestampInSeconds;
        private bool isInitialized;

        public OutfitsRepository(PublishIpfsEntityCommand publishIpfsEntityCommand,
            INftNamesProvider nftNamesProvider,
            ISelfProfile selfProfile)
        {
            this.publishIpfsEntityCommand = publishIpfsEntityCommand;
            this.nftNamesProvider = nftNamesProvider;
            this.selfProfile = selfProfile;
        }

        private static int deployCooldownSeconds
        {
            get
            {
                // Note: the feature flag key and JSON payload keys are owned by the backend
                // team and intentionally still use the original "deploy_window" vocabulary.
                if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.OUTFITS_DEPLOY_WINDOW,
                        "deploy_window_in_seconds",
                        out OutfitsDeployCooldownConfig? config) && config.HasValue && config.Value.CooldownSeconds > 0)
                    return config.Value.CooldownSeconds;

                return DEFAULT_DEPLOY_COOLDOWN_SECONDS;
            }
        }

        [Serializable]
        private struct OutfitsDeployCooldownConfig
        {
            [JsonProperty("deploy_window_in_seconds")] public int CooldownSeconds;
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

            isInitialized = true;
        }

        /// <summary>
        ///     Call before reloading outfits from the server. Blocks publishes until
        ///     <see cref="Initialize" /> seeds a fresh snapshot, so a failed load can never
        ///     lead to publishing from empty or stale state.
        /// </summary>
        public void Invalidate() =>
            isInitialized = false;

        public async UniTask SaveSlotAsync(int slot, OutfitItem item, CancellationToken ct)
        {
            ThrowIfNotInitialized();

            await WaitForPublishCooldownAsync(ct);

            var snapshot = new Dictionary<int, OutfitItem>(committed) { [slot] = item };
            await PublishAsync(snapshot, ct);

            committed[slot] = item;
            lastPublishTimestampInSeconds = NowSeconds();
        }

        public async UniTask DeleteSlotAsync(int slot, CancellationToken ct)
        {
            ThrowIfNotInitialized();

            if (!committed.ContainsKey(slot))
                return;

            await WaitForPublishCooldownAsync(ct);

            var snapshot = new Dictionary<int, OutfitItem>(committed);
            snapshot.Remove(slot);
            await PublishAsync(snapshot, ct);

            committed.Remove(slot);
            lastPublishTimestampInSeconds = NowSeconds();
        }

        // The :outfits entity is full-state replacement: every publish overwrites ALL outfits.
        // Publishing without a server snapshot would silently delete the outfits we never saw.
        private void ThrowIfNotInitialized()
        {
            if (!isInitialized)
                throw new InvalidOperationException("Outfits were never loaded from the server; refusing to publish — it would overwrite unknown server state.");
        }

        /// <summary>
        ///     Throttles consecutive publishes to respect the Catalyst-enforced cooldown between
        ///     <c>:outfits</c> entity deployments. Returns immediately if enough time has already
        ///     passed since the last successful publish; otherwise waits the remainder.
        /// </summary>
        private async UniTask WaitForPublishCooldownAsync(CancellationToken ct)
        {
            int cooldownSeconds = deployCooldownSeconds;
            ulong elapsed = Math.Clamp(NowSeconds() - lastPublishTimestampInSeconds, 0UL, (ulong)cooldownSeconds);
            double remainingSeconds = cooldownSeconds - (double)elapsed;
            if (remainingSeconds <= 0) return;

            await UniTask.Delay(TimeSpan.FromSeconds(remainingSeconds), cancellationToken: ct);
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
            await publishIpfsEntityCommand.ExecuteAsync(outfitsEntity, CancellationToken.None, SERIALIZER_SETTINGS);
        }

        private static ulong NowSeconds() =>
            DateTime.UtcNow.UnixTimeAsMilliseconds() / 1000;
    }
}
