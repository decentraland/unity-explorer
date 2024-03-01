using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using ECS;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class MetaDataSource : IMetaDataSource
    {
        private readonly IRealmData realmData;
        private readonly ICharacterObject characterObject;
        private readonly IPlacesAPIService placesAPIService;

        public MetaDataSource(IRealmData realmData, ICharacterObject characterObject, IPlacesAPIService placesAPIService)
        {
            this.realmData = realmData;
            this.characterObject = characterObject;
            this.placesAPIService = placesAPIService;
        }

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            string parcelId = await ParcelIdAsync(token);
            return new MetaData(realmData.RealmName, parcelId);
        }

        private async UniTask<string> ParcelIdAsync(CancellationToken token)
        {
            Vector3 position = characterObject.Position;
            Vector2Int parcel = ParcelMathHelper.WorldToGridPosition(position);
            PlacesData.PlaceInfo result = await placesAPIService.GetPlaceAsync(parcel, token);
            return result.EnsureNotNull($"parcel not found on coordinates {parcel}").id;
        }
    }
}
