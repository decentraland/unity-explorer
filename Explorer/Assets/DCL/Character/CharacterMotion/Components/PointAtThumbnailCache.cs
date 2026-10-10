using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Components
{
    public struct PointAtThumbnailCache
    {
        public Dictionary<string, Sprite> Cache { get; private set; }

        public static PointAtThumbnailCache Create() =>
            new ()
            {
                Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase)
            };
    }
}
