using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DCL.Backpack.BackpackBus
{
    public readonly struct BackpackEquipCommand
    {
        public readonly string Id;

        public BackpackEquipCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackUnEquipCommand
    {
        public readonly string Id;

        public BackpackUnEquipCommand(string id)
        {
            Id = id;
        }
    }

    public readonly struct BackpackSelectCommand
    {
        public readonly string Id;

        public BackpackSelectCommand(string id)
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
