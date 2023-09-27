using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     Writes the results of the pointer events in the scene world
    ///     <para>
    ///         Must be executed after <see cref="ProcessPointerEventsSystem" />. As they exist in different worlds we must attach them to different
    ///         root system groups as we can't make dependencies between them directly
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    public partial class WritePointerEventResultsSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<RaycastHit> raycastHitPool;
        private readonly IComponentPool<PBPointerEventsResult> pointerEventsResultsPool;
        private readonly ISceneStateProvider sceneStateProvider;

        private uint counter;

        internal WritePointerEventResultsSystem(World world, Entity sceneRoot, IECSToCRDTWriter ecsToCRDTWriter,
            IComponentPool<RaycastHit> raycastHitPool, IComponentPool<PBPointerEventsResult> pointerEventsResultsPool,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.raycastHitPool = raycastHitPool;
            this.pointerEventsResultsPool = pointerEventsResultsPool;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;

            WriteResultsQuery(World, scenePosition);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void WriteResults([Data] Vector3 scenePosition, ref PBPointerEvents pbPointerEvents, ref CRDTEntity sdkEntity)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;

            foreach (byte validIndex in intent.ValidIndices)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];
                PBPointerEvents.Types.Info info = entry.EventInfo;

                RaycastHit sdkHit = raycastHitPool.Get();

                sdkHit.FillSDKRaycastHit(scenePosition, intent.RaycastHit, string.Empty,
                    sdkEntity, intent.Ray.origin, intent.Ray.direction);

                PBPointerEventsResult result = pointerEventsResultsPool.Get();
                result.Hit = sdkHit;
                result.Button = info.Button;
                result.State = entry.EventType;
                result.Timestamp = counter++;
                result.TickNumber = sceneStateProvider.TickNumber;

                ecsToCRDTWriter.AppendMessage(sdkEntity, result);
            }

            intent.ValidIndices.Clear();
        }
    }
}
