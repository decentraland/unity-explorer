using System;
using System.Collections.Generic;

namespace DCL.Profiles
{
    [Serializable]
    public struct IpfsRealmEntity<T>
    {
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
