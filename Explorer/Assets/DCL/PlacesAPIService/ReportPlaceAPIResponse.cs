using System;

namespace DCL.PlacesAPIService
{
    [Serializable]
    public class ReportPlaceAPIResponse
    {
        public bool ok;
        public ReportPlaceResponseData data;
    }

    [Serializable]
    public class ReportPlaceResponseData
    {
        public string filename;
        public string signed_url;
    }
}
