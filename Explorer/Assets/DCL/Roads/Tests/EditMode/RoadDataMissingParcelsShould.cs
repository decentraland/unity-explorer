using System.Collections.Generic;
using System.Linq;
using System.Text;
using DCL.Rendering.GPUInstancing.InstancingData;
using DCL.Roads.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utility;

namespace DCL.Roads.Tests
{
    /// <summary>
    ///     Data regression test for the "Genesis City | Missing Roads" bug
    ///     (decentraland/unity-explorer#3279, genesis-city-missing-roads bug report): 15 Genesis
    ///     City parcels are missing their <see cref="RoadDescription" /> entry in RoadData.asset,
    ///     so those parcels fall out of the road pipeline entirely (classified as a regular scene,
    ///     which then fails its SDK6/LOD fallback) and render as a hole.
    ///     Since PR #3203, road visuals are drawn exclusively from the baked
    ///     <see cref="RoadSettingsAsset.IndirectLODGroups" /> instance buffers (the pooled prefab
    ///     placed by RoadInstantiatorSystem only carries a collider - all its renderers are baked
    ///     disabled), so a fix that adds the <see cref="RoadDescription" /> entries without also
    ///     baking matching GPU-instancing data produces an invisible road with collision. This test
    ///     pins both halves of the fix.
    /// </summary>
    public class RoadDataMissingParcelsShould
    {
        private const string ROAD_DATA_ASSET_PATH = "Assets/DCL/Roads/Settings/RoadData.asset";

        // The 14 parcels reported in unity-explorer#3279 / the #bug-reporting Slack thread, plus
        // 30,-96 (added by the reporter in the Slack thread) and 27,-117 (an additional,
        // unreported instance of the same defect found while investigating - see report.md §3
        // "Drift is wider than the report"). All 15 have an active auto-generated single-parcel
        // road scene deployed on catalyst.
        private static readonly Vector2Int[] EXPECTED_ROAD_COORDS =
        {
            new (45, -117), new (42, -117), new (41, -115), new (41, -113), new (41, -112),
            new (32, -115), new (20, -112), new (30, -117), new (28, -117), new (52, -115),
            new (52, -116), new (3, -92), new (8, -92), new (30, -96), new (27, -117),
        };

        // (43,-112) is privately-owned LAND hosting a 2021 user scene (not an auto-generated road
        // scene) - it was reported alongside the other 14 but is deliberately excluded from the
        // fix, and must never gain a RoadDescription entry.
        private static readonly Vector2Int PRIVATE_LAND_COORD = new (43, -112);

        private RoadSettingsAsset roadDataAsset;

        [SetUp]
        public void SetUp()
        {
            roadDataAsset = AssetDatabase.LoadAssetAtPath<RoadSettingsAsset>(ROAD_DATA_ASSET_PATH);
            Assert.IsNotNull(roadDataAsset, $"Could not load {ROAD_DATA_ASSET_PATH} as a {nameof(RoadSettingsAsset)}");
        }

        [Test]
        public void HaveARoadDescriptionForEveryReportedParcel()
        {
            List<Vector2Int> missing = EXPECTED_ROAD_COORDS
                                       .Where(coord => roadDataAsset.RoadDescriptions.All(d => d.RoadCoordinate != coord))
                                       .ToList();

            Assert.IsEmpty(missing,
                "RoadData.asset is missing a RoadDescription entry for: " + Describe(missing) +
                " (decentraland/unity-explorer#3279 - holes in the Genesis City road network)");
        }

        [Test]
        public void HaveBakedGPUInstancingDataForEveryReportedParcel()
        {
            // Mirrors how RoadsPresence.cs consumes the asset at runtime
            // (gpuInstancingService.AddToIndirect(roadSettingsAsset.IndirectLODGroups)): since
            // PR #3203 this baked buffer - not the RoadDescription list - is what the renderer
            // actually draws, so a RoadDescription with no matching baked instances still renders
            // as an invisible hole with a walkable collider on top (review.md, Finding A).
            List<Vector2Int> withoutBakedInstances = EXPECTED_ROAD_COORDS
                                                     .Where(coord => CountBakedInstancesInsideParcel(coord) == 0)
                                                     .ToList();

            Assert.IsEmpty(withoutBakedInstances,
                "RoadData.asset has a RoadDescription but no baked GPU-instancing data (IndirectLODGroups) for: " +
                Describe(withoutBakedInstances) +
                " - these parcels would render as invisible, collidable holes instead of visible roads");
        }

        [Test]
        public void NotHaveARoadDescriptionForThePrivateLandParcel()
        {
            bool hasDescription = roadDataAsset.RoadDescriptions.Any(d => d.RoadCoordinate == PRIVATE_LAND_COORD);

            Assert.IsFalse(hasDescription,
                $"{PRIVATE_LAND_COORD} is privately-owned LAND with a user scene (not a road) and must not gain a RoadDescription entry");
        }

        /// <summary>
        ///     Counts baked <see cref="PerInstanceBuffer" /> entries (across every
        ///     <see cref="RoadSettingsAsset.IndirectLODGroups" /> group) whose world-space
        ///     translation falls strictly inside this parcel's own 16x16 footprint. A small
        ///     epsilon keeps a shared-corner instance - one that belongs to a neighboring parcel
        ///     but whose translation lands exactly on the boundary the two parcels share - from
        ///     being misattributed to this parcel.
        /// </summary>
        private int CountBakedInstancesInsideParcel(Vector2Int coord)
        {
            const float EPS = 0.5f;

            Vector3 parcelOrigin = coord.ParcelToPositionFlat();
            float minX = parcelOrigin.x + EPS;
            float maxX = parcelOrigin.x + ParcelMathHelper.PARCEL_SIZE - EPS;
            float minZ = parcelOrigin.z + EPS;
            float maxZ = parcelOrigin.z + ParcelMathHelper.PARCEL_SIZE - EPS;

            var count = 0;

            foreach (GPUInstancingLODGroupWithBuffer group in roadDataAsset.IndirectLODGroups)
            {
                if (group.InstancesBuffer == null) continue;

                foreach (PerInstanceBuffer instance in group.InstancesBuffer)
                {
                    Vector4 translation = instance.instMatrix.GetColumn(3);

                    if (translation.x > minX && translation.x < maxX && translation.z > minZ && translation.z < maxZ)
                        count++;
                }
            }

            return count;
        }

        private static string Describe(IEnumerable<Vector2Int> coords)
        {
            var sb = new StringBuilder();
            foreach (Vector2Int coord in coords) sb.Append(coord).Append(' ');
            return sb.ToString();
        }
    }
}
