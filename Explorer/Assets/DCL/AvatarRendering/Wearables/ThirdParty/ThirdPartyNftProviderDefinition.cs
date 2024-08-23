using System;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    [Serializable]
    public struct ThirdPartyProviderListJsonDto
    {
        public ThirdPartyNftProviderDefinition[] data;
    }

    [Serializable]
    public struct ThirdPartyNftProviderDefinition
    {
        public string urn;
        public string name;
        public string description;
    }
}
