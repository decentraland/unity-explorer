using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Systems;
using DCL.Interaction.Utility;
using DCL.InWorldCamera;
using DCL.SyntheticInput.UiSimulation;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using static DCL.SyntheticInput.Systems.SyntheticPointerDiagnostics;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Reads the interaction pipeline's current raycast result into a <see cref="ReticleReading" />. Lives beside
    ///     the systems because the pipeline's components are not visible to the facade's assembly; the facade gets
    ///     <see cref="Read" /> as a <see cref="ReticleProbe" />.
    /// </summary>
    public class SyntheticReticleReader
    {
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<PlayerOriginRaycastResultForSceneEntities>();

        private readonly World globalWorld;
        private readonly IScenesCache scenesCache;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity pipelineEntity;
        private bool resolved;

        public SyntheticReticleReader(World globalWorld, IScenesCache scenesCache)
        {
            this.globalWorld = globalWorld;
            this.scenesCache = scenesCache;
        }

        public ReticleReading Read()
        {
            if (!resolved)
            {
                camera = globalWorld.CacheCamera();
                pipelineEntity = new SingleInstanceEntity(in PIPELINE_ENTITY, globalWorld);
                resolved = true;
            }

            if (!globalWorld.TryGet(camera, out CameraComponent cameraComponent) || !globalWorld.TryGet(camera, out CursorComponent cursor))
                return ReticleReading.Unavailable("no camera entity yet");

            if (cursor.CursorState == CursorState.Panning)
                return ReticleReading.Unavailable("the cursor is panning the camera, so the pipeline builds no reticle ray this frame");

            if (globalWorld.Has<InWorldCameraComponent>(camera))
                return ReticleReading.Unavailable("the in-world camera is open, so the pipeline builds no reticle ray");

            if (!globalWorld.TryGet(pipelineEntity, out PlayerOriginRaycastResultForSceneEntities sceneResult)
                || !globalWorld.TryGet(pipelineEntity, out PlayerOriginRaycastResultForGlobalEntities globalResult)
                || !globalWorld.TryGet(pipelineEntity, out HoverStateComponent hoverState))
                return ReticleReading.Unavailable("the interaction pipeline entity is not created yet");

            ReticleReading reading = ReticleReading.Empty();
            reading.CursorLocked = cursor.CursorState == CursorState.Locked;
            reading.RayFromScreenCenter = cursor.CursorState != CursorState.Free;

            Vector2 rayPixel = reading.RayFromScreenCenter
                ? new Vector2(cameraComponent.Camera.pixelWidth / 2f, cameraComponent.Camera.pixelHeight / 2f)
                : cursor.Position;

            reading.PointerImagePoint = UiScreenGeometry.ScreenToImagePoint(rayPixel);
            reading.Ray = sceneResult.OriginRay;

            if (sceneResult is { IsValidHit: true, EntityInfo: { } entityInfo })
                return FillSceneEntity(ref reading, in sceneResult, in entityInfo, in hoverState);

            if (globalResult.IsValidHit && globalResult.GetEntityInfo() is { } globalEntity)
            {
                reading.Kind = ReticleTargetKind.Avatar;
                reading.GlobalEntityId = globalEntity.EntityReference.Id;
                reading.ColliderName = globalResult.Collider != null ? globalResult.Collider.name : null;
                reading.HitPoint = globalResult.GetRaycastHit().point;
                reading.DistanceToCamera = globalResult.GetDistance();
                return reading;
            }

            // Neither result names an entity: the ray may still have met geometry nobody owns, which is what a human sees.
            if (Physics.Raycast(sceneResult.OriginRay, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK))
            {
                reading.Kind = ReticleTargetKind.World;
                reading.ColliderName = hit.collider.name;
                reading.HitPoint = hit.point;
                reading.DistanceToCamera = hit.distance;
            }

            return reading;
        }

        private ReticleReading FillSceneEntity(ref ReticleReading reading, in PlayerOriginRaycastResultForSceneEntities sceneResult,
            in GlobalColliderSceneEntityInfo entityInfo, in HoverStateComponent hoverState)
        {
            reading.Kind = ReticleTargetKind.SceneEntity;
            reading.SceneEntityId = entityInfo.ColliderSceneEntityInfo.EntityReference.Id;
            reading.CrdtEntityId = entityInfo.ColliderSceneEntityInfo.SDKEntity.Id;
            reading.ColliderName = sceneResult.Collider != null ? sceneResult.Collider.name : null;
            reading.HitPoint = sceneResult.RaycastHit.point;
            reading.DistanceToCamera = sceneResult.GetDistance();
            reading.DistanceToPlayer = sceneResult.DistanceToPlayer;
            reading.HoverQualified = hoverState.HasCollider && hoverState.IsAtDistance && ReferenceEquals(hoverState.LastHitCollider, sceneResult.Collider);

            ISceneFacade? currentScene = scenesCache.CurrentScene.Value;

            if (currentScene != null && ReferenceEquals(currentScene.EcsExecutor.World, entityInfo.EcsExecutor.World))
            {
                reading.SceneId = currentScene.SceneData.SceneEntityDefinition.id;
                reading.SceneName = currentScene.Info.Name;
            }

            IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips = globalWorld.TryGet(pipelineEntity, out HoverFeedbackComponent feedback)
                ? feedback.Tooltips
                : System.Array.Empty<HoverFeedbackComponent.Tooltip>();

            reading.HoverText = ResolveHoverText(in entityInfo, tooltips);

            var tooltipTexts = new string[tooltips.Count];

            for (var i = 0; i < tooltips.Count; i++)
                tooltipTexts[i] = tooltips[i].Text;

            reading.Tooltips = tooltipTexts;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pointerEvents))
                return reading;

            reading.HasPointerEvents = pointerEvents.PointerEvents.Count > 0;

            foreach (PBPointerEvents.Types.Entry? entry in pointerEvents.PointerEvents)
            {
                if (entry != null && InteractionInputUtils.IsQualifiedByDistance(in sceneResult, entry.EventInfo))
                {
                    reading.WithinRange = true;
                    break;
                }
            }

            return reading;
        }
    }
}
