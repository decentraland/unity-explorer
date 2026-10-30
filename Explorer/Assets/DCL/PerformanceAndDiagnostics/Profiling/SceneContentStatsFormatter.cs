using System.Globalization;
using UnityEngine;

namespace DCL.Profiling
{
    /// <summary>
    ///     Per-scene soft caps derived from the parcel count (n), matching the documented Decentraland
    ///     scene limitations (https://docs.decentraland.org/creator/scenes-sdk7/optimizing/scene-limitations/).
    ///     Geometries, colliders and videos have no documented limit. Materials has one, but is shown
    ///     uncapped: URP's SRP Batcher bins draws by shader variant, so per-frame cost tracks shader
    ///     variants, not the raw material count — a cap on materials would mislead more than it informs.
    /// </summary>
    public readonly struct SceneContentCaps
    {
        private const int MAX_TRIANGLES_PER_PARCEL = 10_000;
        private const int MAX_ENTITIES_PER_PARCEL = 200;
        private const int MAX_BODIES_PER_PARCEL = 300;
        private const int MAX_TEXTURES_LOG2_MULTIPLIER = 10;

        public readonly int Entities;
        public readonly long Triangles;
        public readonly int Bodies;
        public readonly int Textures;

        private SceneContentCaps(int entities, long triangles, int bodies, int textures)
        {
            Entities = entities;
            Triangles = triangles;
            Bodies = bodies;
            Textures = textures;
        }

        public static SceneContentCaps ForParcelCount(int parcelCount)
        {
            float log2 = Mathf.Log(parcelCount + 1, 2f);

            return new SceneContentCaps(
                entities: parcelCount * MAX_ENTITIES_PER_PARCEL,
                triangles: (long)parcelCount * MAX_TRIANGLES_PER_PARCEL,
                bodies: parcelCount * MAX_BODIES_PER_PARCEL,
                textures: Mathf.FloorToInt(log2 * MAX_TEXTURES_LOG2_MULTIPLIER));
        }
    }

    /// <summary>
    ///     One rich-text formatted string per <see cref="SceneContentStats" /> row.
    /// </summary>
    public struct SceneContentStatsText
    {
        public string Entities;
        public string Triangles;
        public string Bodies;
        public string Geometries;
        public string Materials;
        public string Textures;
        public string Colliders;
        public string Videos;
    }

    /// <summary>
    ///     Formats <see cref="SceneContentStats" /> rows against <see cref="SceneContentCaps" /> as
    ///     rich-text strings. Capped rows render as "current / cap (percent%)", the whole value white
    ///     below <see cref="CAP_WARNING_PERCENT" /> and orange at or above it — the documented limits
    ///     are soft, so exceeding one warns but never renders red. Uncapped rows render as plain
    ///     counts. Shared by the "Scene content" debug widget and the scene debug menu Scene Stats panel.
    /// </summary>
    public static class SceneContentStatsFormatter
    {
        public const string EMPTY_VALUE = "—";

        private const float CAP_WARNING_PERCENT = 80f;
        private const string WITHIN_BUDGET_COLOR = "#FFFFFF";
        private const string OVER_BUDGET_COLOR = "#FF7439";

        public static void Format(SceneContentStats stats, in SceneContentCaps caps, out SceneContentStatsText text)
        {
            if (!stats.HasData)
            {
                FormatEmpty(out text);
                return;
            }

            text = new SceneContentStatsText
            {
                Entities = FormatCapped(stats.Entities, caps.Entities),
                Triangles = FormatCapped(stats.Triangles, caps.Triangles),
                Bodies = FormatCapped(stats.Bodies, caps.Bodies),
                Geometries = FormatCount(stats.Geometries),
                Materials = FormatCount(stats.Materials),
                Textures = FormatCapped(stats.Textures, caps.Textures),
                Colliders = FormatCount(stats.Colliders),
                Videos = FormatCount(stats.Videos),
            };
        }

        public static void FormatEmpty(out SceneContentStatsText text)
        {
            text = new SceneContentStatsText
            {
                Entities = EMPTY_VALUE,
                Triangles = EMPTY_VALUE,
                Bodies = EMPTY_VALUE,
                Geometries = EMPTY_VALUE,
                Materials = EMPTY_VALUE,
                Textures = EMPTY_VALUE,
                Colliders = EMPTY_VALUE,
                Videos = EMPTY_VALUE,
            };
        }

        private static string FormatCapped(long current, long cap)
        {
            if (cap <= 0)
                return FormatCount(current);

            float percent = current * 100f / cap;
            string percentText = percent.ToString("F0", CultureInfo.InvariantCulture);
            return $"<color={CapColor(percent)}>{FormatCount(current)} / {FormatCount(cap)} ({percentText}%)</color>";
        }

        private static string FormatCount(long current) =>
            current.ToString("N0", CultureInfo.InvariantCulture);

        private static string CapColor(float percent) =>
            percent >= CAP_WARNING_PERCENT ? OVER_BUDGET_COLOR : WITHIN_BUDGET_COLOR;
    }
}
