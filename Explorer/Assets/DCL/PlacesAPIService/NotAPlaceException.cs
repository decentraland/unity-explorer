using System;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public class NotAPlaceException : Exception
    {
        public NotAPlaceException(string placeUUID) : base($"Couldn't find place with ID {placeUUID}") { }

        public NotAPlaceException(Vector2Int coords) : base($"Scene at {coords} is not a place") { }
    }
}
