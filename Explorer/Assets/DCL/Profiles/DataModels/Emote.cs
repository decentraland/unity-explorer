namespace DCL.Profiles
{
    public struct Emote
    {
        public readonly int Slot;
        public readonly string Urn;

        public Emote(int slot, string urn)
        {
            Slot = slot;
            Urn = urn;
        }
    }
}
