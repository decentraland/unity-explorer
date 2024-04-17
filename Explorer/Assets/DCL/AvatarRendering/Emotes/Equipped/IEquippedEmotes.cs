namespace DCL.AvatarRendering.Emotes.Equipped
{
    public interface IEquippedEmotes
    {
        int SlotCount { get; }

        IEmote? EmoteInSlot(int slot);

        bool IsEquipped(IEmote emote);

        int SlotOf(string id);

        int SlotOf(IEmote emote);

        void EquipEmote(int slot, IEmote emote);

        void UnEquipEmote(int slot, IEmote? emote);
    }
}
