using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public interface IEquippedBodyShape
    {
        void Equip(IWearable wearable);

        IWearable? Get();
    }
}
