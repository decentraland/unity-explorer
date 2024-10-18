using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class SceneRoomMetaDataSource : ISceneRoomMetaDataSource
    {
        private readonly IRealmData realmData;
        private readonly IExposedTransform characterTransform;
        private readonly IPlacesAPIService placesAPIService;

        private readonly bool forceSceneIsolation;

        public SceneRoomMetaDataSource(IRealmData realmData, IExposedTransform characterTransform, IPlacesAPIService placesAPIService, bool forceSceneIsolation)
        {
            this.realmData = realmData;
            this.characterTransform = characterTransform;
            this.placesAPIService = placesAPIService;
            this.forceSceneIsolation = forceSceneIsolation;
        }

        public bool ScenesCommunicationIsIsolated => forceSceneIsolation || !realmData.ScenesAreFixed;

        public MetaData.Input GetMetadataInput() =>
            realmData.ScenesAreFixed
                ? new MetaData.Input(realmData.RealmName, Vector2Int.zero)
                : new MetaData.Input(realmData.RealmName, characterTransform.Position.ToParcel());

        public async UniTask<MetaData> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            // Places API is relevant for Genesis City only
            if (realmData.ScenesAreFixed)
                return new MetaData(input.RealmName, input);

            string? id = await ParcelIdAsync(input, token);
            return new MetaData(id, input);
        }

        public bool MetadataIsDirty => !realmData.ScenesAreFixed && characterTransform.Position.IsDirty;

        private async UniTask<string?> ParcelIdAsync(MetaData.Input input, CancellationToken token)
        {
            PlacesData.PlaceInfo? result = await placesAPIService.GetPlaceAsync(input.Parcel, token);
            return result?.id;
        }
    }
}
