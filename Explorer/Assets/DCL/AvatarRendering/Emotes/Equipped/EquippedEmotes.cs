namespace DCL.AvatarRendering.Emotes.Equipped
{
    public class EquippedEmotes : IEquippedEmotes
    {
        private readonly IEmote?[] equippedEmotes = new IEmote[10];

        public int SlotCount => equippedEmotes.Length;

        public IEmote? EmoteInSlot(int slot) =>
            equippedEmotes[slot];

        public bool IsEquipped(IEmote emote)
        {
            foreach (IEmote? equippedEmote in equippedEmotes)
            {
                if (equippedEmote == null) continue;
                if (equippedEmote == emote) return true;
            }

            return false;
        }

        public int SlotOf(string id)
        {
            for (var i = 0; i < equippedEmotes.Length; i++)
                if (equippedEmotes[i]?.GetUrn() == id)
                    return i;

            return -1;
        }

        public int SlotOf(IEmote emote)
        {
            for (var i = 0; i < equippedEmotes.Length; i++)
                if (equippedEmotes[i] == emote)
                    return i;

            return -1;
        }

        public void EquipEmote(int slot, IEmote emote) =>
            equippedEmotes[slot] = emote;

        public void UnEquipEmote(int slot, IEmote? emote) =>
            equippedEmotes[slot] = null;
    }
}
