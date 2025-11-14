using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface ITrimmedWearable : ITrimmedAvatarAttachment
    {
        bool IsCompatibleWithBodyShape(string bodyShape);
    }
}
