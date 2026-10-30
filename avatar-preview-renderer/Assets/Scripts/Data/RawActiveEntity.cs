using System;
using System.Linq;

namespace Data
{
    /// <summary>
    /// Used when decoding base64 entities from the Builder.
    /// Has a different shape than ActiveEntity (e.g. contents is {key, url} objects instead of strings).
    /// </summary>
    [Serializable]
    public class RawActiveEntity
    {
        public string id;
        public Data data;
        public string thumbnail;
        public Translation[] i18n;
        public Data emoteDataADR74;

        // JsonUtility does not support null for custom classes, so we check category
        public bool IsEmote => string.IsNullOrEmpty(data.category);

        [Serializable]
        public class Data
        {
            public string category;
            public Representation[] representations;
            public string[] hides = Array.Empty<string>();
            public string[] replaces = Array.Empty<string>();
            public string[] removesDefaultHiding = Array.Empty<string>();
            public bool loop; // For emotes only
        }

        [Serializable]
        public class Representation
        {
            public string[] bodyShapes;
            public string mainFile;
            public Contents[] contents;
            public string[] overrideHides = Array.Empty<string>();
            public string[] overrideReplaces = Array.Empty<string>();

            [Serializable]
            public class Contents
            {
                public string key;
                public string url;
            }
        }

        [Serializable]
        public class Translation
        {
            public string code;
            public string text;
        }

        static string ExtractHashFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var trimmed = url.TrimEnd('/');
            var slash = trimmed.LastIndexOf('/');
            return slash >= 0 && slash + 1 < trimmed.Length ? trimmed[(slash + 1)..] : null;
        }

        public ActiveEntity ToActiveEntity()
        {
            var reps = IsEmote ? emoteDataADR74.representations : data.representations;

            return new ActiveEntity
            {
                pointers = new[] { id },
                type = IsEmote ? "emote" : "wearable",
                content = reps
                    .SelectMany(r => r.contents
                        .Select(c => new ActiveEntity.Content
                        {
                            file = c.key,
                            url = c.url,
                            // Spring-bone metadata keys models by content hash (CID). The CID is the
                            // last path segment of the builder-api URL — extract it so the lookup works.
                            hash = ExtractHashFromUrl(c.url),
                        }))
                    .GroupBy(c => c.file)
                    .Select(g => g.First())
                    .ToArray(),
                metadata = new ActiveEntity.Metadata
                {
                    id = id,
                    thumbnail = thumbnail,
                    data = IsEmote
                        ? new ActiveEntity.Metadata.Data()
                        : new ActiveEntity.Metadata.Data
                        {
                            category = data.category,
                            hides = data.hides,
                            replaces = data.replaces,
                            removesDefaultHiding = data.removesDefaultHiding,
                            representations = data.representations.Select(r => new ActiveEntity.Metadata.Representation
                            {
                                bodyShapes = r.bodyShapes,
                                mainFile = r.mainFile,
                                overrideHides = r.overrideHides,
                                overrideReplaces = r.overrideReplaces,
                                contents = r.contents.Select(c => c.key).ToArray()
                            }).ToArray()
                        },
                    emoteDataADR74 = !IsEmote
                        ? new ActiveEntity.Metadata.Data()
                        : new ActiveEntity.Metadata.Data
                        {
                            category = emoteDataADR74.category,
                            loop = emoteDataADR74.loop,
                            representations = emoteDataADR74.representations.Select(r =>
                                new ActiveEntity.Metadata.Representation
                                {
                                    bodyShapes = r.bodyShapes,
                                    mainFile = r.mainFile,
                                    contents = r.contents.Select(c => c.key).ToArray()
                                }).ToArray()
                        }
                }
            };
        }
    }
}
