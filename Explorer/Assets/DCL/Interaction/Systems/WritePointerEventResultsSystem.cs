using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
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
        private readonly IPlayerInputEvents playerInputEvents;

        private readonly IComponentPool<RaycastHit> raycastHitPool;

        internal WritePointerEventResultsSystem(World world, ISceneData sceneData, IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider, IGlobalInputEvents globalInputEvents, IComponentPool<RaycastHit> raycastHitPool,
            IPlayerInputEvents playerInputEvents) : base(world)
        {
            this.sceneData = sceneData;
            this.ecsToCRDTWriter = ecsToCRDTWriter;

            this.sceneStateProvider = sceneStateProvider;
            this.globalInputEvents = globalInputEvents;
            this.raycastHitPool = raycastHitPool;
            this.playerInputEvents = playerInputEvents;
        }

        protected override void Update(float t)
        {
            WriteGlobalEvents();
            WriteResultsQuery(World, sceneData.Geometry.BaseParcelPosition);
            WritePlayerInputResultsQuery(World, sceneData.Geometry.BaseParcelPosition);
        }

        private void WriteGlobalEvents()
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < globalInputEvents.Entries.Count; i++)
            {
                if (playerInputEvents.Entries.Exists(e => e.IsAtDistance && e.InputAction == globalInputEvents.Entries[i].InputAction)) {continue;}

                IGlobalInputEvents.Entry entry = globalInputEvents.Entries[i];
                ReportHub.LogError(ReportCategory.INPUT, $"Sent PB pointer event to GLOBAL {SpecialEntitiesID.SCENE_ROOT_ENTITY} - {entry.InputAction} - {entry.PointerEventType}");
                AppendMessage(SpecialEntitiesID.SCENE_ROOT_ENTITY, null, entry.InputAction, entry.PointerEventType);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void WriteResults([Data] Vector3 scenePosition, ref PBPointerEvents pbPointerEvents, ref CRDTEntity sdkEntity)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;

            for (var i = 0; i < intent.ValidIndices.Length; i++)
            {
                byte validIndex = intent.ValidIndices[i];
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];
                PBPointerEvents.Types.Info info = entry.EventInfo;

                RaycastHit raycastHit = raycastHitPool.Get();

                raycastHit.FillSDKRaycastHit(scenePosition, intent.RaycastHit, string.Empty,
                    sdkEntity, intent.Ray.origin, intent.Ray.direction);
                ReportHub.LogError(ReportCategory.INPUT, $"Sent PB pointer event to ENTITY {sdkEntity} - {info.Button} - {entry.EventType}");

                AppendMessage(sdkEntity, raycastHit, info.Button, entry.EventType);
            }

            pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Clear();
        }

        [Query]
        private void WritePlayerInputResults([Data] Vector3 scenePosition)
        {
            if (playerInputEvents.Entries.Count <= 0) return;

            CRDTEntity crdtEntity = playerInputEvents.CrdtEntity;

            foreach (InputEventEntry entry in playerInputEvents.Entries)
            {
                RaycastHit raycastHit = raycastHitPool.Get();

                raycastHit.FillSDKRaycastHit(scenePosition, playerInputEvents.RaycastHit, string.Empty, crdtEntity, playerInputEvents.Ray.origin, playerInputEvents.Ray.direction);
                ReportHub.LogError(ReportCategory.INPUT, $"Sent input action to ENTITY {crdtEntity} - {entry.InputAction} - {entry.PointerEventType}");
                AppendMessage(crdtEntity, raycastHit, entry.InputAction, entry.PointerEventType);
            }

            playerInputEvents.Entries.Clear();
        }

        private void AppendMessage(CRDTEntity sdkEntity, RaycastHit? sdkHit, InputAction button, PointerEventType eventType)
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
