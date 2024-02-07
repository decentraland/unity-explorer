using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public struct IpfsRealmEntity<T>
    {
        public const string DEFAULT_VERSION = "v3";

        [Serializable]
        public struct Files
        {
            public string file;
            public string hash;
        }

        public string version;
        public string type;
        public List<string> pointers;
        public long timestamp;
        public T metadata;
        public List<Files> content;
    }
}
