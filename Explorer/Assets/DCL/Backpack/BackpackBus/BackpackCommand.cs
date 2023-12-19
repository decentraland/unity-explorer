using JetBrains.Annotations;

namespace DCL.Backpack.BackpackBus
{
    public readonly struct BackpackCommand
    {
        public readonly BackpackCommandType Type;
        public readonly string Id;
        public readonly string Category;

        public BackpackCommand(BackpackCommandType type, string id, string category)
        {
            Type = type;
            Id = id;
            Category = category;
        }
    }

    public enum BackpackCommandType
    {
        EquipCommand,
        UnequipCommand,
        HideCommand
    }
}
