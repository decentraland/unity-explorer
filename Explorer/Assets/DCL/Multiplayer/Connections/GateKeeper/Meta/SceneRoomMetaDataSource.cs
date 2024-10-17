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

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            // Places API is relevant for Genesis City only
            if (realmData.ScenesAreFixed)
                return new MetaData(realmData.RealmName, realmData.RealmName, Vector2Int.zero);

            (string? id, Vector2Int parcel) tuple = await ParcelIdAsync(token);
            return new MetaData(realmData.RealmName, tuple.id, tuple.parcel);
        }

        public bool MetadataIsDirty => !realmData.ScenesAreFixed && characterTransform.Position.IsDirty;

        private async UniTask<(string? id, Vector2Int parcel)> ParcelIdAsync(CancellationToken token)
        {
            Vector3 position = characterTransform.Position;
            Vector2Int parcel = position.ToParcel();
            PlacesData.PlaceInfo? result = await placesAPIService.GetPlaceAsync(parcel, token);
            return (result?.id, parcel);
        }
    }
}
