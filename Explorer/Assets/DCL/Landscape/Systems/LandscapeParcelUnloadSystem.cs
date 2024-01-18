using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class LandscapeParcelUnloadSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeAssetPoolManager poolManager;

        public LandscapeParcelUnloadSystem(World world, LandscapeAssetPoolManager poolManager) : base(world)
        {
            this.poolManager = poolManager;
        }

        protected override void Update(float t)
        {
            CleanUpLandscapeParcelsQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpLandscapeParcels(in LandscapeParcel landscapeParcel)
        {
            foreach (KeyValuePair<Transform, List<Transform>> parcelAsset in landscapeParcel.Assets)
            {
                foreach (Transform asset in parcelAsset.Value) { poolManager.Release(parcelAsset.Key, asset); }
            }
        }
    }
}
