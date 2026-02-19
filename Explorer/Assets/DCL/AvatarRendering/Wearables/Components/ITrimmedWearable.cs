using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface ITrimmedWearable : ITrimmedAvatarAttachment
    {
        public int Amount { get; set; }
        bool IsCompatibleWithBodyShape(string bodyShape);

        TrimmedWearableDTO TrimmedDTO { get; }


        public bool IsSmart() =>
            TrimmedDTO.metadata.isSmart;

        void ITrimmedAvatarAttachment.SetAmount(int amount)
        {
            Amount = amount;
        }
    }
}
