using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class CommsInfo
    {
        public string? adapter;
        public string? fixedAdapter;
        public string protocol = string.Empty;

        //for now we only need the adapter, we can add the rest when needed
    }
}
