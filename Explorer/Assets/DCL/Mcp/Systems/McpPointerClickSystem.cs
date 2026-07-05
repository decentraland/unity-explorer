using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Systems;
using DCL.Interaction.Utility;
using DCL.Mcp.Systems.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Mcp.Systems
{
    /// <summary>
    ///     <para>
    ///         Delivers an agent-requested pointer press to a scene entity while a <see cref="McpPointerClickIntent" />
    ///         is present on the player entity. The aim is validated with the same physics raycast the reticle
    ///         pipeline uses (camera origin, <see cref="PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK" />, occlusion and
    ///         max-distance rules apply), then the target's <see cref="PBPointerEvents.AppendPointerEventResultsIntent" />
    ///         is filled exactly as <see cref="ProcessPointerEventsSystem" /> fills it for a real click, so the
    ///         unmodified scene-world write-back emits an identical PBPointerEventsResult.
    ///     </para>
    ///     <para>
    ///         Runs after <see cref="ProcessPointerEventsSystem" /> so its per-frame intent Initialize cannot wipe
    ///         the synthetic press before the scene-world flush, which happens later in the same frame.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class McpPointerClickSystem : BaseUnityLoopSystem
    {
        private const float MAX_RAYCAST_DISTANCE = 100f;

        private static readonly QueryDescription ALL_ENTITIES = new ();

        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Entity playerEntity;

        private SingleInstanceEntity playerCamera;

        internal McpPointerClickSystem(World world,
            IScenesCache scenesCache,
            IEntityCollidersGlobalCache collidersGlobalCache,
            Entity playerEntity) : base(world)
        {
            this.scenesCache = scenesCache;
            this.collidersGlobalCache = collidersGlobalCache;
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            base.Initialize();
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ref McpPointerClickIntent intent = ref World.TryGetRef<McpPointerClickIntent>(playerEntity, out bool exists);

            if (!exists)
                return;

            if (UnityEngine.Time.time > intent.Deadline)
            {
                CompleteAndRemove(intent.Completion, Failure(in intent, "click timed out before it could be delivered (is the simulation paused?)"));
                return;
            }

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null || !scene.SceneStateProvider.IsCurrent || scene.SceneStateProvider.IsNotRunningState())
            {
                CompleteAndRemove(intent.Completion, Failure(in intent, "no running current scene to deliver the click to"));
                return;
            }

            World sceneWorld = scene.EcsExecutor.World;

            if (intent.SceneWorld != null && !ReferenceEquals(sceneWorld, intent.SceneWorld))
            {
                CompleteAndRemove(intent.Completion, Failure(in intent, "the scene reloaded mid-click; only the press may have been delivered"));
                return;
            }

            switch (intent.Phase)
            {
                case McpPointerClickIntent.ClickPhase.Down:
                    PointerEventType pressType = intent.Kind == McpPointerClickIntent.ClickKind.Up
                        ? PointerEventType.PetUp
                        : PointerEventType.PetDown;

                    if (!TryDeliver(ref intent, sceneWorld, pressType, out McpPointerClickResult result))
                    {
                        CompleteAndRemove(intent.Completion, result);
                        return;
                    }

                    if (intent.Kind != McpPointerClickIntent.ClickKind.Click)
                    {
                        CompleteAndRemove(intent.Completion, result);
                        return;
                    }

                    intent.SceneWorld = sceneWorld;
                    intent.DownTick = scene.SceneStateProvider.TickNumber;
                    intent.DownResult = result;
                    intent.Phase = McpPointerClickIntent.ClickPhase.WaitTick;
                    return;

                case McpPointerClickIntent.ClickPhase.WaitTick:
                    // The scene must observe PetDown on an earlier tick than PetUp, otherwise ordering is ambiguous.
                    if (scene.SceneStateProvider.TickNumber > intent.DownTick)
                        intent.Phase = McpPointerClickIntent.ClickPhase.Up;

                    return;

                case McpPointerClickIntent.ClickPhase.Up:
                    DeliverUp(ref intent, sceneWorld, out McpPointerClickResult upResult);
                    CompleteAndRemove(intent.Completion, upResult);
                    return;
            }
        }

        /// <summary>Structural removal happens only after every read of the intent ref is done.</summary>
        private void CompleteAndRemove(UniTaskCompletionSource<McpPointerClickResult>? completion, McpPointerClickResult result)
        {
            World.Remove<McpPointerClickIntent>(playerEntity);
            completion?.TrySetResult(result);
        }

        private static McpPointerClickResult Failure(in McpPointerClickIntent intent, string reason) =>
            new ()
            {
                Hit = false,
                FailureReason = reason,
                SceneEntityId = intent.TargetEntityId,
            };

        private bool TryDeliver(ref McpPointerClickIntent intent, World sceneWorld, PointerEventType eventType, out McpPointerClickResult result)
        {
            if (!TryResolveTarget(ref intent, sceneWorld, out result))
                return false;

            Entity targetEntity = intent.ResolvedEntity;
            bool requireTarget = intent.TargetEntityId >= 0;

            Vector3 aimPoint = intent.HasExplicitAimPoint
                ? intent.AimPoint
                : ResolveEntityAimPoint(sceneWorld, targetEntity);

            CameraComponent camera = playerCamera.GetCameraComponent(World);
            Vector3 origin = camera.Camera.transform.position;
            Vector3 direction = aimPoint - origin;

            if (direction.sqrMagnitude < 0.0001f)
            {
                result = Failure(in intent, "the camera is on top of the aim point; move back and retry");
                return false;
            }

            var ray = new Ray(origin, direction.normalized);

            if (!Physics.Raycast(ray, out RaycastHit hit, MAX_RAYCAST_DISTANCE, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK))
            {
                result = Failure(in intent, "the ray from the camera hit nothing (target may lack a collider)");
                return false;
            }

            if (!collidersGlobalCache.TryGetSceneEntity(hit.collider, out GlobalColliderSceneEntityInfo hitInfo)
                || !ReferenceEquals(hitInfo.EcsExecutor.World, sceneWorld))
            {
                result = Failure(in intent, $"the ray hit a non-scene collider '{hit.collider.name}'");
                return false;
            }

            Entity hitEntity = hitInfo.ColliderSceneEntityInfo.EntityReference;

            if (requireTarget && hitEntity != targetEntity)
            {
                result = Failure(in intent, "another collider blocks the line of sight to the target");
                result.BlockedByEntityId = hitEntity.Id;
                result.BlockedByCrdtId = hitInfo.ColliderSceneEntityInfo.SDKEntity.Id;
                result.BlockedByColliderName = hit.collider.name;
                return false;
            }

            // In pure aim-point mode the raycast decides the target.
            targetEntity = hitEntity;
            intent.ResolvedEntity = hitEntity;

            if (!hitInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents))
            {
                result = Failure(in intent, $"entity {targetEntity.Id} has no PointerEvents component (not clickable)");
                result.SceneEntityId = targetEntity.Id;
                return false;
            }

            if (!sceneWorld.TryGet(targetEntity, out CRDTEntity crdtEntity))
            {
                result = Failure(in intent, $"entity {targetEntity.Id} has no CRDTEntity; the scene cannot receive results for it");
                result.SceneEntityId = targetEntity.Id;
                return false;
            }

            if (!IsQualified(pbPointerEvents!, ray, hit, camera, out float distance, out string? hoverText, out bool hasCursorEntry))
            {
                result = Failure(in intent, hasCursorEntry
                    ? $"target is out of range for its pointer events (hit distance {distance:F2}m)"
                    : "the target's pointer events are proximity-type only; a cursor click cannot trigger them");

                result.SceneEntityId = targetEntity.Id;
                result.CrdtEntityId = crdtEntity.Id;
                result.Distance = distance;
                return false;
            }

            pbPointerEvents!.AppendPointerEventResultsIntent.Initialize(hit, ray);
            pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(intent.Button, eventType);

            intent.DownHit = hit;
            intent.DownRay = ray;

            result = new McpPointerClickResult
            {
                Hit = true,
                SceneEntityId = targetEntity.Id,
                CrdtEntityId = crdtEntity.Id,
                HoverText = hoverText,
                HitPoint = hit.point,
                Distance = distance,
            };

            return true;
        }

        /// <summary>
        ///     Delivers the release. If the target moved out from under the ray after the press (or its distance gate
        ///     no longer qualifies), the press-frame hit is reused so the entity still receives an ordered PetUp,
        ///     and the divergence is reported via <see cref="McpPointerClickResult.UpRayMissed" />.
        /// </summary>
        private void DeliverUp(ref McpPointerClickIntent intent, World sceneWorld, out McpPointerClickResult result)
        {
            McpPointerClickResult downResult = intent.DownResult!;

            if (TryDeliver(ref intent, sceneWorld, PointerEventType.PetUp, out McpPointerClickResult freshResult))
            {
                result = freshResult;
                return;
            }

            // Fresh delivery failed: fall back to the press-frame hit if the component is still reachable.
            if (sceneWorld.IsAlive(intent.ResolvedEntity) && sceneWorld.TryGet(intent.ResolvedEntity, out PBPointerEvents? pbPointerEvents) && pbPointerEvents != null)
            {
                pbPointerEvents.AppendPointerEventResultsIntent.Initialize(intent.DownHit, intent.DownRay);
                pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(intent.Button, PointerEventType.PetUp);

                downResult.UpRayMissed = true;
                result = downResult;
                return;
            }

            downResult.UpRayMissed = true;
            downResult.FailureReason = $"the entity disappeared after the press ({freshResult.FailureReason}); only PetDown was delivered";
            result = downResult;
        }

        private bool TryResolveTarget(ref McpPointerClickIntent intent, World sceneWorld, out McpPointerClickResult result)
        {
            result = null!;

            // Aim-point mode: the validation raycast picks the entity.
            if (intent.HasExplicitAimPoint && intent.TargetEntityId < 0)
                return true;

            if (intent.SceneWorld != null)
            {
                if (sceneWorld.IsAlive(intent.ResolvedEntity))
                    return true;

                result = Failure(in intent, "the target entity was destroyed mid-click");
                return false;
            }

            Entity found = Entity.Null;
            int targetId = intent.TargetEntityId;

            sceneWorld.Query(in ALL_ENTITIES, entity =>
            {
                if (entity.Id == targetId)
                    found = entity;
            });

            if (found == Entity.Null)
            {
                result = Failure(in intent, $"no entity with id {targetId} in the current scene world");
                return false;
            }

            intent.ResolvedEntity = found;
            return true;
        }

        /// <summary>Aim at the collider volume when available; entity pivots can sit at hinges or bases and miss.</summary>
        private static Vector3 ResolveEntityAimPoint(World sceneWorld, Entity entity)
        {
            if (sceneWorld.TryGet(entity, out PrimitiveColliderComponent primitiveCollider) && primitiveCollider.Collider != null)
                return primitiveCollider.Collider.bounds.center;

            if (sceneWorld.TryGet(entity, out TransformComponent transformComponent) && transformComponent.Transform != null)
                return transformComponent.Transform.position;

            return Vector3.zero;
        }

        /// <summary>
        ///     Mirrors the cursor-entry qualification of <see cref="ProcessPointerEventsSystem" />: entries get their
        ///     defaults prepared and the distance gate is evaluated per entry, the last cursor entry winning, exactly
        ///     like the production loop. Also picks the hover text a real reticle hover would show for this button.
        /// </summary>
        private bool IsQualified(PBPointerEvents pbPointerEvents, in Ray ray, in RaycastHit hit, in CameraComponent camera, out float distance, out string? hoverText, out bool hasCursorEntry)
        {
            distance = camera.Mode == CameraMode.FirstPerson
                ? hit.distance
                : Vector3.Distance(hit.point, camera.PlayerFocus.position);

            float? playerDistance = null;

            if (World.TryGet(playerEntity, out CharacterTransform characterTransform))
                playerDistance = Vector3.Distance(hit.point, characterTransform.Position);

            var raycastResult = new PlayerOriginRaycastResultForSceneEntities();
            raycastResult.SetRay(ray);
            raycastResult.SetupHit(hit, default(GlobalColliderSceneEntityInfo), distance, playerDistance);

            var isAtDistance = false;
            hoverText = null;
            hasCursorEntry = false;

            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[i]!;

                if (entry.InteractionType != InteractionType.Cursor)
                    continue;

                hasCursorEntry = true;

                PBPointerEvents.Types.Info info = entry.EventInfo!;
                info.PrepareDefaultValues();

                isAtDistance = InteractionInputUtils.IsQualifiedByDistance(in raycastResult, info);

                if (!isAtDistance)
                    continue;

                if (hoverText == null && info.HasHoverText && !string.IsNullOrEmpty(info.HoverText))
                    hoverText = info.HoverText;
            }

            return isAtDistance;
        }
    }
}
