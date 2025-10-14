using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Profiles;
using DCL.Web3;
using ECS;

namespace DCL.Backpack.AvatarSection.Outfits.Repository
{
    using OutfitsEntity = EntityDefinitionGeneric<OutfitsMetadata>;

    /// <summary>
    ///     Handles the deployment of user outfits to the Catalyst network by constructing
    ///     and publishing an Outfits entity, mirroring the logic of RealmProfileRepository.
    /// </summary>
    public class OutfitsRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS =
            new ()
            {
                Converters = new JsonConverter[]
                {
                    new OutfitsMetadataConverter()
                }
            };

        private readonly IRealmData realm;
        private readonly INftNamesProvider nftNamesProvider;

        public OutfitsRepository(IRealmData realm,
            INftNamesProvider nftNamesProvider)
        {
            this.realm = realm;
            this.nftNamesProvider = nftNamesProvider;
        }

        /// <summary>
        ///     Deploys the complete set of outfits for a user to the Catalyst network.
        /// </summary>
        public async UniTask SetAsync(Profile? profile, List<OutfitItem> outfits, CancellationToken ct, bool noExtraSlots = false)
        {
            if (realm is { Configured: false })
                return;

            if (profile == null)
                throw new ArgumentException("Cannot save outfits for a null profile");

            if (string.IsNullOrEmpty(profile?.UserId))
                throw new ArgumentException("Cannot save outfits for a user with an empty UserId");

            var namesForExtraSlots = await nftNamesProvider.GetAsync(new Web3Address(profile.UserId), 1, 1, ct);
            var metadata = new OutfitsMetadata
            {
                outfits = outfits, namesForExtraSlots = noExtraSlots ? new List<string>() : new List<string>(namesForExtraSlots.Names)
            };

            var outfitsEntity = new OutfitsEntity(string.Empty, metadata)
            {
                version = OutfitsEntity.DEFAULT_VERSION, pointers = new[]
                {
                    $"{profile.UserId}:outfits"
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), type = IpfsRealmEntityType.Outfits.ToEntityString(), content = Array.Empty<ContentDefinition>()
            };

            try
            {
                await realm.Ipfs.PublishAsync(outfitsEntity, ct, new Dictionary<string, byte[]>());
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                throw;
            }
        }
    }
}