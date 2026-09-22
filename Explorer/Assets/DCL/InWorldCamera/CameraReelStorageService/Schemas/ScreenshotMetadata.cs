using System;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace DCL.InWorldCamera.CameraReelStorageService.Schemas
{
    // Server schema: decentraland/camera-reel-service/src/api.rs#/Metadata
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

    // Server schema: decentraland/camera-reel-service/src/api.rs#/Scene
    [Serializable]
    public class Scene
    {
        public string name = null!;
        public Location location = null!;
    }

    // Server schema: decentraland/camera-reel-service/src/api.rs#/Location
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

    // Server schema: decentraland/camera-reel-service/src/api.rs#/User
    [Serializable]
    public class VisiblePerson
    {
        public string userName = null!;
        public string userAddress = null!;
        public bool isGuest;
        public bool isEmoting;

        /// <summary>
        /// Where this person stands in the saved photo: normalized to the image, with the origin at its
        /// top-left corner. Zero when the person is in frame but their bounds could not be projected.
        /// </summary>
        public Rect screenRect;

        public string[] wearables = null!;
    }
}
