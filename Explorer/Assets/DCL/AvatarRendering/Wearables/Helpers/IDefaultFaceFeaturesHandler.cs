using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IDefaultFaceFeaturesHandler
    {
        FacialFeaturesTextures GetDefaultFacialFeaturesDictionary(BodyShape bodyShape);
    }
}
