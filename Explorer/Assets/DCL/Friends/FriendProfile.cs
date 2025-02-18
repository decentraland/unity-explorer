using CommunicationData.URLHelpers;
using DCL.Web3;
using UnityEngine;

namespace DCL.Friends
{
    public class FriendProfile
    {
        public Color UserNameColor { get; }
        public Web3Address Address { get; }
        public string Name { get; }
        public bool HasClaimedName { get; }
        public URLAddress FacePictureUrl { get; }

        public FriendProfile(Web3Address address, string name, bool hasClaimedName, URLAddress facePictureUrl, Color userNameColor)
        {
            Address = address;
            Name = name;
            HasClaimedName = hasClaimedName;
            FacePictureUrl = facePictureUrl;
            UserNameColor = userNameColor;
        }

        private bool Equals(FriendProfile other) =>
            Address.Equals(other.Address);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FriendProfile) obj);
        }

        public override int GetHashCode() =>
            Address.GetHashCode();
    }
}
