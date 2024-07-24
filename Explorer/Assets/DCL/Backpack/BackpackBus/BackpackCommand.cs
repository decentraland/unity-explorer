using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DCL.Backpack.BackpackBus
{
    public readonly struct BackpackEmoteSlotSelectCommand
    {
        public readonly int Slot;

        public BackpackEmoteSlotSelectCommand(int slot)
        {
            Slot = slot;
        }
    }

    public readonly struct BackpackEquipEmoteCommand
    {
        public readonly string Id;
        public readonly int? Slot;
        public readonly bool IsManuallyEquipped; // It will be true when the user manually equip an emote from the emotes grid

        public BackpackEquipEmoteCommand(string id, int? slot, bool isManuallyEquipped)
        {
            Id = id;
            Slot = slot;
            IsManuallyEquipped = isManuallyEquipped;
        }
    }

    public readonly struct BackpackUnEquipEmoteCommand
    {
        public readonly string? Id;
        public readonly int? Slot;

        public BackpackUnEquipEmoteCommand(string? id = null, int? slot = null)
        {
            Id = id;
            Slot = slot;
        }
    }

    public readonly struct BackpackEquipWearableCommand
    {
        public readonly string Id;

        public BackpackEquipWearableCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackUnEquipWearableCommand
    {
        public readonly string Id;

        public BackpackUnEquipWearableCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackSelectWearableCommand
    {
        public readonly string Id;

        public BackpackSelectWearableCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackSelectEmoteCommand
    {
        public readonly string Id;

        public BackpackSelectEmoteCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackHideCommand
    {
        public readonly IReadOnlyCollection<string> ForceRender;
        public readonly bool IsInitialHide;

        public BackpackHideCommand(IReadOnlyCollection<string> forceRender, bool isInitialHide = false)
        {
            ForceRender = new ReadOnlyCollection<string>(forceRender.ToList());
            IsInitialHide = isInitialHide;
        }
    }

    public readonly struct BackpackFilterCategoryCommand
    {
        public readonly string Category;
        public readonly AvatarWearableCategoryEnum CategoryEnum;

        public BackpackFilterCategoryCommand(string category, AvatarWearableCategoryEnum categoryEnum = AvatarWearableCategoryEnum.Body)
        {
            Category = category;
            CategoryEnum = categoryEnum;
        }
    }

    public readonly struct BackpackSearchCommand
    {
        public readonly string SearchText;

        public BackpackSearchCommand(string searchText)
        {
            SearchText = searchText;
        }
    }

    public readonly struct BackpackChangeColorCommand
    {
        public readonly Color NewColor;
        public readonly string Category;

        public BackpackChangeColorCommand(Color newColor, string category)
        {
            NewColor = newColor;
            Category = category;
        }
    }

    public readonly struct BackpackUnEquipAllCommand { }

    public readonly struct BackpackPublishProfileCommand { }
}
