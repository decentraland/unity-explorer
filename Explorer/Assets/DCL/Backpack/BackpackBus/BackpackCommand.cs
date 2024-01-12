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
        public readonly string Category;
        public readonly bool? IsHidden;

        public BackpackHideCommand(string category, bool? isHidden)
        {
            Category = category;
            IsHidden = isHidden;
        }
    }

    public readonly struct BackpackFilterCategoryCommand
    {
        public readonly string Category;

        public BackpackFilterCategoryCommand(string category)
        {
            Category = category;
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
}
