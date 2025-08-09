using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetInvitableCommunityListResponse : ISerializationCallbackReceiver
    {
        [Serializable]
        public class InvitableCommunityData
        {
            public string communityId;
            public string communityName;
        }

        private string[] communityNames;

        public InvitableCommunityData[] data;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            communityNames = new string[data.Length];
            for (int i = 0; i < data.Length; i++)
                communityNames[i] = data[i].communityName;
        }
    }
}
