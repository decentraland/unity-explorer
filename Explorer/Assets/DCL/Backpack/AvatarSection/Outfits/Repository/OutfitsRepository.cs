using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Profiles;
using DCL.Web3;
using ECS;
using Newtonsoft.Json;
using Utility;
using Utility.Json;
using Utility.Times;

namespace DCL.Backpack.AvatarSection.Outfits.Repository
{
    using OutfitsEntity = EntityDefinitionGeneric<OutfitsMetadata>;

    /// <summary>
    ///     Handles the deployment of user outfits to the Catalyst network by constructing
    ///     and publishing an Outfits entity, mirroring the logic of RealmProfileRepository.
    /// </summary>
    public class OutfitsRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new ()
            { Converters = new List<JsonConverter> { new ColorJsonConverter() } };

        private const int DEPLOY_WINDOW_IN_SECONDS = 15;

        private readonly PublishIpfsEntityCommand publishIpfsEntityCommand;
        private readonly INftNamesProvider nftNamesProvider;

        private ulong passedTimeSinceLastDeployment = 0;
        private ulong lastDeployTimestampInSeconds = 0;

        private UniTaskCompletionSource? currentResolutionTask;
        private List<OutfitItem>? currentOutfits;
        private int currentVersion;

        public OutfitsRepository(PublishIpfsEntityCommand publishIpfsEntityCommand,
            INftNamesProvider nftNamesProvider)
        {
            this.publishIpfsEntityCommand = publishIpfsEntityCommand;
            this.nftNamesProvider = nftNamesProvider;
        }

        /// <summary>
        ///     Deploys the complete set of outfits for a user to the Catalyst network.
        ///     Multiple rapid calls are coalesced: only the latest state is deployed,
        ///     after a 15-second delay that respects the backend rate limit.
        /// </summary>
        public async UniTask SetAsync(Profile? profile, List<OutfitItem> outfits, CancellationToken ct)
        {
            if (profile == null)
                throw new ArgumentException("Cannot save outfits for a null profile");

            if (string.IsNullOrEmpty(profile?.UserId))
                throw new ArgumentException("Cannot save outfits for a user with an empty UserId");

            currentOutfits = outfits;
            currentVersion++;

            if (currentResolutionTask != null)
            {
                await UniTask.WhenAny(currentResolutionTask.Task, UniTask.WaitUntilCanceled(ct));
                return;
            }

            currentResolutionTask = new UniTaskCompletionSource();

            try
            {
                passedTimeSinceLastDeployment = Math.Clamp((DateTime.UtcNow.UnixTimeAsMilliseconds() / 1000) - lastDeployTimestampInSeconds, 0, DEPLOY_WINDOW_IN_SECONDS);

                int localVersion;

                do
                {
                    localVersion = currentVersion;
                    List<OutfitItem> localOutfits = currentOutfits!;

                    await UniTask.Delay(TimeSpan.FromSeconds(DEPLOY_WINDOW_IN_SECONDS - passedTimeSinceLastDeployment), cancellationToken: ct);

                    INftNamesProvider.PaginatedNamesResponse namesForExtraSlots = await nftNamesProvider.GetAsync(new Web3Address(profile.UserId), 1, 1, ct);

                    var metadata = new OutfitsMetadata
                    {
                        outfits = localOutfits, namesForExtraSlots = namesForExtraSlots.Names.Count > 0
                            ? new List<string>
                            {
                                namesForExtraSlots.Names[0],
                            }
                            : new List<string>(),
                    };

                    var outfitsEntity = new OutfitsEntity(string.Empty, metadata)
                    {
                        version = OutfitsEntity.DEFAULT_VERSION, pointers = new[]
                        {
                            $"{profile.UserId}:outfits",
                        },
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), type = IpfsRealmEntityType.Outfits.ToEntityString(), content = Array.Empty<ContentDefinition>(),
                    };

                    await publishIpfsEntityCommand.ExecuteAsync(outfitsEntity, ct, SERIALIZER_SETTINGS);
                    passedTimeSinceLastDeployment = 0;
                }
                while (localVersion != currentVersion);

                currentResolutionTask.TrySetResult();
            }
            catch (Exception e)
            {
                currentResolutionTask.TrySetException(e);
                throw;
            }
            finally
            {
                currentResolutionTask = null;
                lastDeployTimestampInSeconds = DateTime.UtcNow.UnixTimeAsMilliseconds() / 1000;
            }
        }
    }
}
