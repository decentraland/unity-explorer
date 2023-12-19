namespace DCL.Backpack.BackpackBus
{
    public readonly struct EquipCommand
    {
        public readonly string Id;

        public EquipCommand(string id)
        {
            Id = id;
        }
    }
}
