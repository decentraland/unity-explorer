using UnityEngine;

namespace DCL.Donations.UI
{
    public readonly struct DonationsPanelParameter
    {
        public readonly string CreatorAddress;
        public readonly Vector2Int BaseParcel;
        public readonly bool HasValues;

        private DonationsPanelParameter(string creatorAddress, Vector2Int baseParcel, bool hasValues)
        {
            CreatorAddress = creatorAddress;
            BaseParcel = baseParcel;
            HasValues = hasValues;
        }

        public static DonationsPanelParameter Empty => new (string.Empty, Vector2Int.zero, false);
        public static DonationsPanelParameter Create(string creatorAddress, Vector2Int baseParcel) =>
            new (creatorAddress, baseParcel, true);
    }
}
