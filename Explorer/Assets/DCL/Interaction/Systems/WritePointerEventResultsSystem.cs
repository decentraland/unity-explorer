using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Systems;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using UnityEngine;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     Writes the results of the pointer events in the scene world
    ///     <para>
    ///         Must be executed after <see cref="Interaction.Systems.ProcessPointerEventsSystem" />. As they exist in different worlds we must attach them to different
    ///         root system groups as we can't make dependencies between them directly
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class WritePointerEventResultsSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IGlobalInputEvents globalInputEvents;

        private readonly IComponentPool<RaycastHit> raycastHitPool;


        internal WritePointerEventResultsSystem(World world, ISceneData sceneData, IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider, IGlobalInputEvents globalInputEvents, IComponentPool<RaycastHit> raycastHitPool) : base(world)
        {
            this.sceneData = sceneData;
            this.ecsToCRDTWriter = ecsToCRDTWriter;

            this.sceneStateProvider = sceneStateProvider;
            this.globalInputEvents = globalInputEvents;
            this.raycastHitPool = raycastHitPool;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var messageSent = false;
            WriteResultsQuery(World, sceneData.Geometry.BaseParcelPosition, ref messageSent);

            if (!messageSent)
                WriteGlobalEvents();
        }

        private void WriteGlobalEvents()
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < globalInputEvents.Entries.Count; i++)
            {
                IGlobalInputEvents.Entry entry = globalInputEvents.Entries[i];
                AppendMessage(SpecialEntitiesID.SCENE_ROOT_ENTITY, null, entry.InputAction, entry.PointerEventType);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void WriteResults([Data] Vector3 scenePosition, [Data] ref bool messageSent, ref PBPointerEvents pbPointerEvents, ref CRDTEntity sdkEntity)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;

            foreach (byte validIndex in intent.ValidIndices)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];
                PBPointerEvents.Types.Info info = entry.EventInfo;

                RaycastHit raycastHit = raycastHitPool.Get();

                raycastHit.FillSDKRaycastHit(scenePosition, intent.RaycastHit, string.Empty,
                    sdkEntity, intent.Ray.origin, intent.Ray.direction);

                AppendMessage(sdkEntity, raycastHit, info.Button, entry.EventType);
            }
            pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Clear();

            if (intent.ValidInputActions != null)
            {
                foreach (var inputAction in intent.ValidInputActions)
                {
                    RaycastHit raycastHit = raycastHitPool.Get();

                    raycastHit.FillSDKRaycastHit(scenePosition, intent.RaycastHit, string.Empty,
                        sdkEntity, intent.Ray.origin, intent.Ray.direction);

                    AppendMessage(sdkEntity, raycastHit, inputAction.Key, inputAction.Value);

                    //We dont consider hover events to disable global input messages
                    if (inputAction.Value != PointerEventType.PetHoverEnter && inputAction.Value != PointerEventType.PetHoverLeave) { messageSent = true; }
                }
                pbPointerEvents.AppendPointerEventResultsIntent.ValidInputActions.Clear();
            }
        }

        private void AppendMessage(CRDTEntity sdkEntity, RaycastHit sdkHit, InputAction button, PointerEventType eventType)
        {
            ecsToCRDTWriter.AppendMessage<PBPointerEventsResult, (RaycastHit? sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>(
                static (result, data) =>
                {
                    result.Hit = data.sdkHit;
                    result.Button = data.button;
                    result.State = data.eventType;
                    result.Timestamp = data.sceneStateProvider.TickNumber;
                    result.TickNumber = data.sceneStateProvider.TickNumber;
                }, sdkEntity, (int)sceneStateProvider.TickNumber, (sdkHit, button, eventType, sceneStateProvider));
        }
    }
}
