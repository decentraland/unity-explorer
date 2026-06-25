using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityPlacesResponse
    {
        public GetCommunityPlacesData? data;
    }

    [Serializable]
    public class GetCommunityPlacesData
    {
        public GetCommunityPlacesResult[] results = Array.Empty<GetCommunityPlacesResult>();
        public int total;
        public int limit;
        public int offset;
    }

    [Serializable]
    public class GetCommunityPlacesResult
    {
        public string id;
    }
}
