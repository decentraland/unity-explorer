using System;

namespace Data
{
    [Serializable]
    public class ActiveEntity
    {
        public string id;
        public string[] pointers;
        public string type;
        public Content[] content;
        public Metadata metadata;

        public bool IsEmote => type == "emote";

        [Serializable]
        public class Content
        {
            public string file;
            public string hash;

            public string url; // Used by Base64 entities only
        }

        [Serializable]
        public class Metadata
        {
            public string id;
            public string name;
            public string thumbnail;
            public Data data;
            // ReSharper disable once InconsistentNaming
            public Data emoteDataADR74;

            public Translation[] i18n;

            [Serializable]
            public class Data
            {
                public string category;
                public Representation[] representations;
                public string[] hides = Array.Empty<string>();
                public string[] replaces = Array.Empty<string>();
                public string[] removesDefaultHiding = Array.Empty<string>();
                public bool loop; // For emotes only
                public SpringBonesDto springBones;
            }
            
            [Serializable]
            public class Representation
            {
                public string[] bodyShapes;
                public string mainFile;
                public string[] contents = Array.Empty<string>();
                public string[] overrideHides = Array.Empty<string>();
                public string[] overrideReplaces = Array.Empty<string>();
            }

            [Serializable]
            public class Translation
            {
                public string code;
                public string text;
            }
        }
    }
}