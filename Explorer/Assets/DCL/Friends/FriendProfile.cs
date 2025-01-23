using CommunicationData.URLHelpers;
using DCL.Web3;

namespace DCL.Friends
{
    public class FriendProfile
    {
        public Web3Address Address { get; private set; }
        public string Name { get; private set; }
        public bool HasClaimedName { get; private set; }
        public URLAddress FacePictureUrl { get; private set; }

        public FriendProfile(Web3Address address, string name, bool hasClaimedName, URLAddress facePictureUrl)
        {
            Address = address;
            Name = name;
            HasClaimedName = hasClaimedName;
            FacePictureUrl = facePictureUrl;
        }
    }
}
