using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Models;
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

        public OutfitsRepository(IRealmData realm)
        {
            this.realm = realm;
        }

        /// <summary>
        ///     Deploys the complete set of outfits for a user to the Catalyst network.
        /// </summary>
        public async UniTask SetAsync(string userId, List<OutfitItem> outfits, CancellationToken ct)
        {
            if (realm is { Configured: false })
                return;

            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("Cannot save outfits for a user with an empty UserId");

            var metadata = new OutfitsMetadata
            {
                outfits = outfits, namesForExtraSlots = new List<string>()
            };

            var outfitsEntity = new OutfitsEntity(string.Empty, metadata)
            {
                version = OutfitsEntity.DEFAULT_VERSION, pointers = new[]
                {
                    $"{userId}:outfits"
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), type = IpfsRealmEntityType.Outfits.ToEntityString(), content = Array.Empty<ContentDefinition>() // No thumbnails for now
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