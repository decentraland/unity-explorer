using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
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
    [LogCategory(ReportCategory.INPUT)]
    public partial class WritePointerEventResultsSystem : BaseUnityLoopSystem
    {
        private static readonly PBPointerEventsResult SHARED_POINTER_EVENTS_RESULT = new ();
        private static readonly RaycastHit SHARED_RAYCAST_HIT = new RaycastHit().Reset();

        private readonly Entity sceneRoot;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IGlobalInputEvents globalInputEvents;

        //private uint counter;

        internal WritePointerEventResultsSystem(World world, Entity sceneRoot, IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider, IGlobalInputEvents globalInputEvents) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.ecsToCRDTWriter = ecsToCRDTWriter;

            this.sceneStateProvider = sceneStateProvider;
            this.globalInputEvents = globalInputEvents;
        }

        protected override void Update(float t)
        {
            Vector3 scenePosition = World.Get<TransformComponent>(sceneRoot).Cached.WorldPosition;
            WriteResultsQuery(World, scenePosition);
            WriteGlobalEvents();

            //counter++;
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
        private void WriteResults([Data] Vector3 scenePosition, ref PBPointerEvents pbPointerEvents, ref CRDTEntity sdkEntity)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;

            foreach (byte validIndex in intent.ValidIndices)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];
                PBPointerEvents.Types.Info info = entry.EventInfo;

                SHARED_RAYCAST_HIT.FillSDKRaycastHit(scenePosition, intent.RaycastHit, string.Empty,
                    sdkEntity, intent.Ray.origin, intent.Ray.direction);

                AppendMessage(sdkEntity, SHARED_RAYCAST_HIT, info.Button, entry.EventType);
            }

            pbPointerEvents.AppendPointerEventResultsIntent.ValidIndices.Clear();
        }

        private void AppendMessage(CRDTEntity sdkEntity, RaycastHit sdkHit, InputAction button, PointerEventType eventType)
        {
            PBPointerEventsResult result = SHARED_POINTER_EVENTS_RESULT;
            result.Hit = sdkHit;
            result.Button = button;
            result.State = eventType;
            result.Timestamp = sceneStateProvider.TickNumber;
            result.TickNumber = sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage(sdkEntity, result, (int)result.Timestamp);
        }
    }
}
