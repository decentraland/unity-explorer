using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public readonly struct BackpackEquipOutfitCommand
    {
        public readonly string BodyShape;
        public readonly IReadOnlyCollection<string> Wearables;
        public readonly Color EyesColor;
        public readonly Color HairColor;
        public readonly Color SkinColor;
        public readonly IReadOnlyCollection<string> ForceRender;
        public readonly bool UseFullUrns;
        public readonly Action? OnEnd;

        public BackpackEquipOutfitCommand(string bodyShape,
            IReadOnlyCollection<string> wearables,
            Color eyesColor,
            Color hairColor,
            Color skinColor,
            IReadOnlyCollection<string> forceRender,
            bool useFullUrns = false,
            Action? onEnd = null)
        {
            BodyShape = bodyShape;
            Wearables = wearables;
            EyesColor = eyesColor;
            HairColor = hairColor;
            SkinColor = skinColor;
            ForceRender = forceRender;
            UseFullUrns = useFullUrns;
            OnEnd = onEnd;
        }
    }
}
