using System;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class ProfileResponse
    {
        public Avatar[] avatars;

        [Serializable]
        public class Avatar
        {
            public AvatarData avatar;

            [Serializable]
            public class AvatarData
            {
                public string bodyShape;
                public string[] wearables = Array.Empty<string>();
                public string[] forceRender = Array.Empty<string>();
                public ColorData eyes;
                public ColorData hair;
                public ColorData skin;
                public Snapshot snapshots;

                public BodyShape GetBodyShape()
                {
                    return bodyShape.Equals(WearablesConstants.BODY_SHAPE_MALE, StringComparison.OrdinalIgnoreCase)
                        ? BodyShape.Male
                        : BodyShape.Female;
                }

                public AvatarColors GetAvatarColors()
                {
                    return new AvatarColors(eyes.color, hair.color, skin.color);
                }
                
                [Serializable]
                public class ColorData
                {
                    public Color color;
                }

                [Serializable]
                public class Snapshot
                {
                    public string face256;
                    public string body;
                }
            }
        }
    }
}