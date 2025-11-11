using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public class EquippedWearables : IEquippedWearables
    {
        private readonly Dictionary<string, IWearable?> wearables = new ();
        private readonly HashSet<string> forceRenderCategories = new ();
        public IReadOnlyCollection<string> ForceRenderCategories => forceRenderCategories;

        private Color hairColor;
        private Color eyesColor;
        private Color bodyshapeColor;

        public EquippedWearables()
        {
            foreach (string category in WearableCategories.CATEGORIES_PRIORITY)
                wearables.Add(category, null);
        }

        public IWearable? Wearable(string category) =>
            wearables[category];

        public (Color, Color, Color) GetColors() =>
            (hairColor, eyesColor, bodyshapeColor);

        public bool IsEquipped(ITrimmedWearable wearable) =>
            wearables[wearable.GetCategory()]?.DTO.id == wearable.TrimmedDTO.id;

        public void Equip(IWearable wearable) =>
            wearables[wearable.GetCategory()] = wearable;

        public void UnEquip(IWearable wearable)
        {
            IAvatarAttachment attachment = wearable;
            if (IsEquipped(wearable) == false)
                throw new InvalidOperationException($"Trying to unequip a wearable that is not equipped. {attachment.GetCategory()}");

            wearables[attachment.GetCategory()] = null;
        }

        public void UnEquipAll()
        {
            foreach (string category in WearableCategories.CATEGORIES_PRIORITY)
                wearables[category] = null;
        }

        public void SetHairColor(Color newColor) =>
            hairColor = newColor;

        public void SetEyesColor(Color newColor) =>
            eyesColor = newColor;

        public void SetBodyshapeColor(Color newColor) =>
            bodyshapeColor = newColor;

        public void SetForceRender(IReadOnlyCollection<string> categories)
        {
            forceRenderCategories.Clear();
            foreach (string category in categories) { forceRenderCategories.Add(category); }
        }

        public IReadOnlyDictionary<string, IWearable?> Items() =>
            wearables;
    }
}
