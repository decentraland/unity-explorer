using System;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public struct EmoteJsonDTO
    {
        //hash
        public string id;
        public string type;
        public string[] pointers;
        public long timestamp;
        public string version;
        public Metadata metadata;
        public Content[] content;

        [Serializable]
        public struct Content
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public struct Metadata
        {
            public Data data;

            //urn
            public string id;
            public string name;

            public I18n[] i18n;
            public string thumbnail;

            public string rarity;
            public string description;

            [Serializable]
            public struct I18n
            {
                public string code;
                public string text;
            }

            [Serializable]
            public struct Representation
            {
                public string[] bodyShapes;
                public string mainFile;
                public string[] contents;
                public string[] overrideHides;
                public string[] overrideReplaces;
            }

            [Serializable]
            public struct Data
            {
                public Representation[] representations;
                public string category;
                public string[] tags;
                public string[] replaces;
                public string[] hides;
                public string[] removesDefaultHiding;
                public bool loop;
            }
        }
    }
}
