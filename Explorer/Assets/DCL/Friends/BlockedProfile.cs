using CommunicationData.URLHelpers;
using DCL.Web3;
using System;
using UnityEngine;

namespace DCL.Friends
{
    public class BlockedProfile : FriendProfile
    {
        public DateTime Timestamp { get; }

        public BlockedProfile(Web3Address address,
            string name,
            bool hasClaimedName,
            URLAddress facePictureUrl,
            DateTime timestamp,
            Color userNameColor)
            : base(address, name, hasClaimedName, facePictureUrl, userNameColor)
        {
            Timestamp = timestamp;
        }
    }
}
