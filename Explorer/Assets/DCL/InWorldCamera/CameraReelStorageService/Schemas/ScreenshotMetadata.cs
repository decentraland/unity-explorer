using System;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace DCL.InWorldCamera.CameraReelStorageService.Schemas
{
    // Server schema: https://github.com/decentraland/camera-reel-service/blob/main/src/api.rs (struct Metadata)
    [Serializable]
    public class ScreenshotMetadata
    {
        public string userName = null!;
        public string userAddress = null!;
        public string dateTime = null!;
        public string placeId = null!;
        public string realm = null!;
        public Scene scene = null!;
        public VisiblePerson[] visiblePeople = null!;
    }

    // Server schema: https://github.com/decentraland/camera-reel-service/blob/main/src/api.rs (struct Scene)
    [Serializable]
    public class Scene
    {
        public string name = null!;
        public Location location = null!;
    }

    // Server schema: https://github.com/decentraland/camera-reel-service/blob/main/src/api.rs (struct Location)
    [Serializable]
    public class Location
    {
        public string x = null!;
        public string y = null!;

        public Location(Vector2Int position)
        {
            x = position.x.ToString();
            y = position.y.ToString();
        }
    }

    // Server schema: https://github.com/decentraland/camera-reel-service/blob/main/src/api.rs (struct User)
    [Serializable]
    public class VisiblePerson
    {
        public string userName = null!;
        public string userAddress = null!;
        public bool isGuest;
        public bool isEmoting;

        /// <summary>
        /// Where this person stands in the saved photo: normalized to the image, with the origin at its
        /// top-left corner. Zero when their bounds do not cover any area of the image.
        /// </summary>
        public Rect screenRect;

        public string[] wearables = null!;
    }
}
