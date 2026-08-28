using UnityEngine;

namespace Data
{
    public class AvatarColors
    {
        public Color Eyes { get; }
        public Color Hair { get; }
        public Color Skin { get; }

        public AvatarColors(Color eyes, Color hair, Color skin)
        {
            Eyes = eyes;
            Hair = hair;
            Skin = skin;
        }
    }
}