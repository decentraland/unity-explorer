using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService.Schemas
{
    [Serializable]
    public class ScreenshotMetadata
    {
        public string userName;
        public string userAddress;
        public string dateTime;
        public string realm;
        public Scene scene;
        public VisiblePerson[] visiblePeople;
    }

    [Serializable]
    public class Scene
    {
        public string name;
        public Location location;
    }

    [Serializable]
    public class Location
    {
        public string x;
        public string y;

        public Location(Vector2Int position)
        {
            x = position.x.ToString();
            y = position.y.ToString();
        }
    }

    [Serializable]
    public class VisiblePerson
    {
        public string userName;
        public string userAddress;
        public bool isGuest;

        public string[] wearables;
    }
}
