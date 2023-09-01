using DCL.AvatarRendering.AvatarShape;
using UnityEngine;

public static class AvatarPoolUtils
{
    //TODO: How can I get the addressable resource here??
    public static AvatarBase CreateAvatarContainer() =>
        Object.Instantiate(Resources.Load<AvatarBase>("AvatarBase"), Vector3.zero, Quaternion.identity);
}
