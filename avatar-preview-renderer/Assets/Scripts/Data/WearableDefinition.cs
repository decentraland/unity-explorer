using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data
{
    public class WearableDefinition
    {
        public string Pointer { get; private set; }
        public string Category { get; private set; }
        public string Thumbnail { get; private set; }
        public string BodyShape { get; private set; }

        public Dictionary<string, string> Files { get; private set; }
        public string MainFile { get; private set; }

        public string[] Hides { get; private set; }
        public string[] Replaces { get; private set; }
        public string[] RemovesDefaultHiding { get; private set; }

        public bool HasValidRepresentation { get; private set; } = true;

        public static WearableDefinition FromActiveEntity(ActiveEntity entity, string bodyShape)
        {
            var representation =
                entity.metadata.data.representations.FirstOrDefault(r => r.bodyShapes.Contains(bodyShape));
            var hasCorrectRepresentation = representation != null;
            if (representation == null)
            {
                Debug.LogError("No representation found for body shape: " + bodyShape);
                representation = entity.metadata.data.representations.First();
            }

            return new WearableDefinition
            {
                Pointer = entity.pointers[0],
                Category = entity.metadata.data.category,
                Thumbnail = entity.metadata.thumbnail,
                BodyShape = bodyShape,
                RemovesDefaultHiding = entity.metadata.data.removesDefaultHiding,
                HasValidRepresentation = hasCorrectRepresentation,
                Files = entity.content.Where(c => representation.contents.Contains(c.file))
                    .ToDictionary(c => c.file, c => c.url ?? c.hash),
                MainFile = representation.mainFile,

                // If the representation has hides we take it from there, otherwise we take it from the entity metadata
                Hides = representation.overrideHides is { Length: > 0 }
                    ? representation.overrideHides
                    : entity.metadata.data.hides,

                // If the representation has replaces we take it from there, otherwise we take it from the entity metadata
                Replaces = representation.overrideReplaces is { Length: > 0 }
                    ? representation.overrideReplaces
                    : entity.metadata.data.replaces
            };
        }
    }
}