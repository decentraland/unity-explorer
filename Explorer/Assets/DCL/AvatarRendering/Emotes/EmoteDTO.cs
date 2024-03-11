using System;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public struct EmoteDTO
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

        public void Sanitize()
        {
            metadata.data.hides = Array.Empty<string>();
            metadata.data.replaces = Array.Empty<string>();
            metadata.data.removesDefaultHiding = Array.Empty<string>();

            for (var i = 0; i < metadata.data.representations.Length; i++)
            {
                metadata.data.representations[i].overrideHides = Array.Empty<string>();
                metadata.data.representations[i].overrideReplaces = Array.Empty<string>();
            }
        }
    }
}
