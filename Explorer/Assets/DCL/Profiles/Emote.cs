namespace DCL.Profiles
{
    public class Emote
    {
        public int Slot { get; }
        public string Urn { get; }

        public Emote(int slot, string urn)
        {
            Slot = slot;
            Urn = urn;
        }
    }
}
