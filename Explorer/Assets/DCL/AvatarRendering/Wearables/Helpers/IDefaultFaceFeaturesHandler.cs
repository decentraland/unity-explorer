using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IDefaultFaceFeaturesHandler
    {
        Dictionary<string, Texture> GetDefaultFacialFeaturesDictionary(BodyShape bodyShape);
    }
}