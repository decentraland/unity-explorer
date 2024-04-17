using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Equipped
{
    public class EquippedBodyShape : IEquippedBodyShape
    {
        private IWearable? bodyShape;

        public void Equip(IWearable wearable) =>
            bodyShape = wearable;

        public IWearable? Get() =>
            bodyShape;
    }
}
