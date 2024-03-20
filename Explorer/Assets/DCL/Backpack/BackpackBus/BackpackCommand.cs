using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DCL.Backpack.BackpackBus
{
    public readonly struct BackpackEquipEmoteCommand
    {
        public readonly string Id;
        public readonly int Slot;

        public BackpackEquipEmoteCommand(string id, int slot)
        {
            Id = id;
            Slot = slot;
        }
    }

    public readonly struct BackpackUnEquipEmoteCommand
    {
        public readonly int Slot;

        public BackpackUnEquipEmoteCommand(int slot)
        {
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

        public BackpackHideCommand(IReadOnlyCollection<string> forceRender)
        {
            ForceRender = new ReadOnlyCollection<string>(forceRender.ToList());
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

    public readonly struct BackpackPublishProfileCommand { }
}
