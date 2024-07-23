using DCL.Character;
using UnityEngine;
using Utility;

namespace SceneRunner.Debugging.Hub
{
    public class LocationBasedWorldInfoHub : IWorldInfoHub
    {
        private readonly IWorldInfoHub origin;
        private readonly ICharacterObject characterObject;

        public LocationBasedWorldInfoHub(IWorldInfoHub origin, ICharacterObject characterObject)
        {
            this.origin = origin;
            this.characterObject = characterObject;
        }

        public IWorldInfo? WorldInfo(string sceneName)
        {
            var result = origin.WorldInfo(sceneName);

            if (result is null && sceneName is "CURRENT")
            {
                Vector2Int parcel = ParcelMathHelper.FloorToParcel(characterObject.Position);
                return WorldInfo(parcel);
            }

            return result;
        }

        public IWorldInfo? WorldInfo(Vector2Int coordinates) =>
            origin.WorldInfo(coordinates);
    }
}
