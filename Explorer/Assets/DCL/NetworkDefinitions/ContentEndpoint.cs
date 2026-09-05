using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class ContentEndpoint
    {
        public bool healthy;
        public string publicUrl;

        public ContentEndpoint() : this(string.Empty) { }

        public ContentEndpoint(string publicUrl, bool healthy = true)
        {
            this.healthy = healthy;
            this.publicUrl = publicUrl;
        }
    }
}
