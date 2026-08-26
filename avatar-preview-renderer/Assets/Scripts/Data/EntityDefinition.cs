using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Runtime.Wearables;
using Services;
using UnityEngine;
using Utils;

namespace Data
{
    public class EntityDefinition
    {
        public readonly string URN;
        public readonly string Category;
        public readonly string Thumbnail;
        public readonly EntityType Type;
        public readonly EntityFlags Flags;

        [CanBeNull] private readonly SpringBonesDto _springBones;

        [ItemCanBeNull] private readonly Dictionary<BodyShape, Representation> _representations;

        /// <summary>
        /// Returns the spring-bone params (boneName → params) declared in the wearable
        /// metadata for the given body shape's mainFile, or null if none.
        /// </summary>
        [CanBeNull]
        public IReadOnlyDictionary<string, SpringBoneParamsDto> GetSpringBoneParams(BodyShape shape)
        {
            if (_springBones?.models == null) return null;
            if (!HasRepresentation(shape)) return null;

            var mainFileHash = _representations[shape].MainFileHash;
            if (string.IsNullOrEmpty(mainFileHash)) return null;
            return _springBones.models.TryGetValue(mainFileHash, out var map) ? map : null;
        }

        public Representation[] GetAllRepresentations()
        {
            // TODO: Cleanup
            if (_representations[BodyShape.Female] != null && _representations[BodyShape.Male] != null)
            {
                return new Representation[] { _representations[BodyShape.Female],  _representations[BodyShape.Male] };
            } else if (_representations[BodyShape.Male] != null)
            {
                return new Representation[] { _representations[BodyShape.Male] };
            } else if (_representations[BodyShape.Female] != null)
            {
                return new Representation[] { _representations[BodyShape.Female] };
            }
            else
            {
                return new Representation[] { };
            }
        }

        public bool HasRepresentation(BodyShape bodyShape)
        {
            return _representations[bodyShape] != null;
        }

        public Representation this[BodyShape shape] => _representations[shape] ?? throw new InvalidOperationException(
            $"Missing {shape} representation for {URN}");

        private EntityDefinition(string urn, string category, string thumbnail, EntityType type, EntityFlags flags,
            Dictionary<BodyShape, Representation> representations, [CanBeNull] SpringBonesDto springBones)
        {
            URN = urn;
            Category = category;
            Thumbnail = thumbnail;
            Type = type;
            _representations = representations;
            Flags = flags;
            _springBones = springBones;
        }

        public class Representation
        {
            public readonly Dictionary<string, string> Files;
            public readonly string MainFile;
            public readonly string MainFileHash;
            public readonly string[] Hides;
            public readonly string[] RemovesDefaultHiding;

            private Representation(Dictionary<string, string> files, string mainFile, string mainFileHash,
                string[] hides, string[] removesDefaultHiding)
            {
                Files = files;
                MainFile = mainFile;
                MainFileHash = mainFileHash;
                Hides = hides;
                RemovesDefaultHiding = removesDefaultHiding;
            }

            [CanBeNull]
            public static Representation ForBodyShape(string bodyShape, ActiveEntity entity)
            {
                var data = entity.IsEmote ? entity.metadata.emoteDataADR74 : entity.metadata.data;

                var entityRepresentation =
                    data.representations.FirstOrDefault(r => r.bodyShapes.Contains(bodyShape));

                if (entityRepresentation == null)
                    return null;

                var repContents = entityRepresentation.contents ?? Array.Empty<string>();
                var repContentsSet = new HashSet<string>(repContents, StringComparer.OrdinalIgnoreCase);

                var entityFilesSet = new HashSet<string>(
                    entity.content.Select(c => c.file),
                    StringComparer.OrdinalIgnoreCase
                );

                var main = entityRepresentation.mainFile;
                var hasMain = !string.IsNullOrWhiteSpace(main);
                var mainInRepContents = hasMain && repContentsSet.Contains(main);
                var mainInEntityContent = hasMain && entityFilesSet.Contains(main);

                if (!(hasMain && mainInRepContents && mainInEntityContent))
                {
                    Debug.Log($"[BodyShapeSanitizer] Dropping rep for bodyShape={bodyShape}, mainFile='{main}' (not valid)");
                    return null;
                }

                var filesDict = entity.content
                    .Where(c => repContentsSet.Contains(c.file))
                    .ToDictionary(
                        c => c.file,
                        c => c.url ?? string.Format(APIService.APICatalyst, c.hash),
                        StringComparer.OrdinalIgnoreCase
                    );

                var mainFileHash = entity.content
                    .FirstOrDefault(c => string.Equals(c.file, main, StringComparison.OrdinalIgnoreCase))?.hash;

                var representation = new Representation(
                    filesDict,
                    entityRepresentation.mainFile,
                    mainFileHash,
                    entityRepresentation.overrideHides is { Length: > 0 }
                        ? entityRepresentation.overrideHides
                        : data.hides.Union(entityRepresentation.overrideReplaces is { Length: > 0 }
                            ? entityRepresentation.overrideReplaces
                            : data.replaces).Distinct().ToArray(),
                    data.removesDefaultHiding ?? Array.Empty<string>()
                );

                return representation;
            }



            public static Representation ForEmbeddedEmote(string emote)
            {
                return new Representation(
                    new Dictionary<string, string>
                    {
                        ["main"] = Path.Combine(Application.streamingAssetsPath, $"{emote}.glb")
                    },
                    "main", null, Array.Empty<string>(),
                    Array.Empty<string>()
                );
            }
        }

        public static EntityDefinition FromActiveEntity(ActiveEntity entity)
        {
            var urn = entity.pointers[0];
            var category = entity.IsEmote ? "emote" : entity.metadata.data.category;
            var thumbnailContent = entity.content?.FirstOrDefault(c => c.file == entity.metadata.thumbnail);
            var thumbnail = thumbnailContent != null
                ? string.Format(APIService.APICatalyst, thumbnailContent.hash)
                : null;
            var type = entity.IsEmote ? EntityType.Emote :
                urn.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase) || urn.Equals(
                    WearablesConstants.BODY_SHAPE_MALE, StringComparison.OrdinalIgnoreCase)  ? EntityType.Body :
                WearableCategories.FACIAL_FEATURES.Contains(category) ? EntityType.FacialFeature : EntityType.Wearable;
            var flags = entity.IsEmote && entity.metadata.emoteDataADR74.loop ? EntityFlags.Looping : EntityFlags.None;

            var representations = new Dictionary<BodyShape, Representation>
            {
                [BodyShape.Male] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_MALE, entity),
                [BodyShape.Female] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_FEMALE, entity)
            };

            return new EntityDefinition(urn, category, thumbnail, type, flags, representations,
                entity.IsEmote ? null : entity.metadata?.data?.springBones);
        }

        public static EntityDefinition FromBase64(byte[] b64)
        {
            var base64String = Encoding.UTF8.GetString(b64);

            // Base64 JSON uses {key, url} objects for contents instead of plain strings,
            // so we parse into RawActiveEntity which handles that format
            var raw = JsonUtility.FromJson<RawActiveEntity>(base64String);
            var entity = raw.ToActiveEntity();

            // JsonUtility can't deserialize the nested Dictionary in springBones.models,
            // so re-parse just that field with Newtonsoft and inject it into the entity.
            if (!entity.IsEmote && entity.metadata?.data != null)
            {
                try
                {
                    var jo = Newtonsoft.Json.Linq.JObject.Parse(base64String);
                    var springBonesToken = jo["data"]?["springBones"];
                    if (springBonesToken != null)
                        entity.metadata.data.springBones = springBonesToken.ToObject<SpringBonesDto>();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Base64] failed to parse springBones: {e.Message}");
                }
            }

            return FromActiveEntity(entity);
        }

        public static EntityDefinition FromEmbeddedEmote(string emote, bool loop)
        {
            return new EntityDefinition(
                $"embedded:{emote}",
                "emote",
                null,
                EntityType.Emote,
                loop ? EntityFlags.Looping : EntityFlags.None,
                new Dictionary<BodyShape, Representation>
                {
                    [BodyShape.Male] = Representation.ForEmbeddedEmote(emote),
                    [BodyShape.Female] = Representation.ForEmbeddedEmote(emote)
                },
                null
            );
        }
    }

    [Flags]
    public enum EntityFlags : byte
    {
        None = 0,
        Looping = 1 << 0,
    }

    public enum EntityType
    {
        Body,
        Wearable,
        FacialFeature,
        Emote
    }

    public enum BodyShape
    {
        Male,
        Female
    }
}