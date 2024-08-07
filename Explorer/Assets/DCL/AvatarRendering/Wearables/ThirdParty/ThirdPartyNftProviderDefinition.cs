using System;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    [Serializable]
    public struct ThirdPartyProviderListJsonDto
    {
        public ThirdPartyNftProviderDefinition[] thirdPartyProviders;
    }

    [Serializable]
    public struct ThirdPartyNftProviderDefinition
    {
        public string id;
        public Metadata metadata;
        // TODO: add more fields if required

        [Serializable]
        public struct Metadata
        {
            public ThirdParty thirdParty;

            [Serializable]
            public struct ThirdParty
            {
                public string name;
                public string description;
            }
        }
    }
}
