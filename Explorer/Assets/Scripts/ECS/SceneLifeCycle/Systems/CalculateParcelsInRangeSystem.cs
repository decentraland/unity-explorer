using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Calculates parcels in range and cache them in component for future usage in the current frame
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [Obsolete("No longer used by Increasing Radius Systems")]
    public partial class CalculateParcelsInRangeSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;

        internal CalculateParcelsInRangeSystem(World world, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            Vector3 position = World.Get<TransformComponent>(playerEntity).Transform.position;

            ForEachRealmQuery(World, position);
        }

        [Query]
        private void ForEachRealm([Data] Vector3 playerPosition, ref ParcelsInRange parcelsInRange)
        {
            ParcelMathHelper.ParcelsInRange(playerPosition, parcelsInRange.LoadRadius, parcelsInRange.Value);
        }
    }
}
