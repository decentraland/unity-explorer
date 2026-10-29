using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;

namespace DCL.Backpack.Tests
{
    internal static class RandomizerTestWearables
    {
        private static readonly string[] BOTH_BODY_SHAPES = { BodyShape.MALE, BodyShape.FEMALE };

        public static ITrimmedWearable Trimmed(string urn, string category, bool isSmart = false, string[]? bodyShapes = null) =>
            new TrimmedFake(urn, category, isSmart, bodyShapes ?? BOTH_BODY_SHAPES);

        public static ITrimmedWearable[] Trimmed(string urnPrefix, string category, int count, bool isSmart = false)
        {
            var wearables = new ITrimmedWearable[count];

            for (var i = 0; i < count; i++)
                wearables[i] = Trimmed($"{urnPrefix}{i}", category, isSmart);

            return wearables;
        }

        public static IWearable Full(string urn, string category, params string[] hides) =>
            new Wearable(new StreamableLoadingResult<WearableDTO>(new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                        hides = hides,
                        replaces = Array.Empty<string>(),
                        representations = new[]
                        {
                            new AvatarAttachmentDTO.Representation
                            {
                                bodyShapes = BOTH_BODY_SHAPES,
                                overrideHides = Array.Empty<string>(),
                                overrideReplaces = Array.Empty<string>(),
                            },
                        },
                    },
                },
            }));

        private sealed class TrimmedFake : ITrimmedWearable
        {
            private readonly string[] bodyShapes;

            public TrimmedFake(string urn, string category, bool isSmart, string[] bodyShapes)
            {
                this.bodyShapes = bodyShapes;

                TrimmedDTO = new TrimmedWearableDTO
                {
                    id = urn,
                    metadata = new TrimmedWearableDTO.WearableMetadataDto
                    {
                        id = urn,
                        isSmart = isSmart,
                        data = new TrimmedWearableDTO.WearableMetadataDto.DataDto { category = category },
                    },
                };
            }

            public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
            public TrimmedWearableDTO TrimmedDTO { get; }
            public StreamableLoadingResult<TrimmedWearableDTO> TrimmedModel { get; set; }
            TrimmedAvatarAttachmentDTO ITrimmedAvatarAttachment.TrimmedDTO => TrimmedDTO;
            public int Amount { get; set; }

            public bool IsCompatibleWithBodyShape(string bodyShape) =>
                Array.IndexOf(bodyShapes, bodyShape) >= 0;
        }
    }

    /// <summary>
    ///     Returns the same value for every odds roll and the same index, clamped to the range, for every index.
    /// </summary>
    internal sealed class ScriptedRandom : Random
    {
        private readonly int nextIndex;
        private readonly double nextDouble;

        public ScriptedRandom(int nextIndex, double nextDouble = 0)
        {
            this.nextIndex = nextIndex;
            this.nextDouble = nextDouble;
        }

        public override double NextDouble() =>
            nextDouble;

        public override int Next(int maxValue) =>
            Math.Min(nextIndex, maxValue - 1);
    }
}
