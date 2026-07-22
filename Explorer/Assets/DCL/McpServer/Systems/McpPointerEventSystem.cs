using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Physics;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Systems;
using DCL.Interaction.Utility;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     <para>
    ///         Delivers a single agent-requested pointer event to a scene entity while a
    ///         <see cref="McpEcsPointerEventIntent" /> is present on the player entity. The aim is validated with the
    ///         same physics raycast the reticle pipeline uses (camera origin, <see cref="PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK" />,
    ///         occlusion and max-distance rules apply), then the target's <see cref="PBPointerEvents.AppendPointerEventResultsIntent" />
    ///         is filled exactly as <see cref="ProcessPointerEventsSystem" /> fills it for a real press, so the
    ///         unmodified scene-world write-back emits an identical PBPointerEventsResult.
    ///     </para>
    ///     <para>
    ///         A release that follows a press (<see cref="McpEcsPointerEventIntent.Press" />) is delivered only once
    ///         the scene has advanced past the press tick and only to the world that received the press; the
    ///         click_entity tool composes a full click from two such intents.
    ///     </para>
    ///     <para>
    ///         Runs after <see cref="ProcessPointerEventsSystem" /> so its per-frame intent Initialize cannot wipe
    ///         the synthetic event before the scene-world flush, which happens later in the same frame.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class McpPointerEventSystem : BaseUnityLoopSystem
    {
        private const float MAX_RAYCAST_DISTANCE = 100f;
        private const float AIM_DIRECTION_EPSILON_SQR = 0.0001f;

        private static readonly QueryDescription ALL_ENTITIES = new ();

        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Entity playerEntity;

        private SingleInstanceEntity playerCamera;

        internal McpPointerEventSystem(World world,
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
            ref McpEcsPointerEventIntent intent = ref World.TryGetRef<McpEcsPointerEventIntent>(playerEntity, out bool exists);

            if (!exists)
                return;

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null || !scene.SceneStateProvider.IsCurrent || scene.SceneStateProvider.IsNotRunningState())
            {
                CompleteAndRemove(in intent, Failure(in intent, "no running current scene to deliver the pointer event to"));
                return;
            }

            if (intent.SceneId != null && !IsPinnedScene(scene, intent.SceneId))
            {
                CompleteAndRemove(in intent, Failure(in intent, $"the request is pinned to scene '{intent.SceneId}' but the current scene is '{scene.Info.Name}' (did the player move?)"));
                return;
            }

            World sceneWorld = scene.EcsExecutor.World;
            uint tick = scene.SceneStateProvider.TickNumber;

            if (intent.Press.HasValue)
            {
                McpPressHandoff press = intent.Press.Value;

                // A mid-click reload swaps in a new world for the same parcel; the press handoff belongs to the
                // disposed one (entity ids get recycled), so the release can only be failed.
                if (!ReferenceEquals(sceneWorld, press.World))
                {
                    CompleteAndRemove(in intent, Failure(in intent, "the scene reloaded mid-click; only the press may have been delivered"));
                    return;
                }

                // The scene must observe the press on an earlier tick than the release, otherwise ordering is ambiguous.
                if (tick <= press.Tick)
                    return;

                DeliverRelease(in intent, in press, sceneWorld, tick, out McpPointerClickResult releaseResult);
                CompleteAndRemove(in intent, releaseResult);
                return;
            }

            TryDeliver(in intent, sceneWorld, tick, null, out McpPointerClickResult result);
            CompleteAndRemove(in intent, result);
        }

        /// <summary>The pin matches only when its id resolves to the very scene that is current.</summary>
        private bool IsPinnedScene(ISceneFacade currentScene, string sceneId) =>
            scenesCache.TryGetBySceneId(sceneId, out ISceneFacade? pinned) && ReferenceEquals(pinned, currentScene);

        /// <summary>The intent is copied out before the structural removal, so the caller's ref must not be touched afterwards.</summary>
        private void CompleteAndRemove(in McpEcsPointerEventIntent intent, McpPointerClickResult result) =>
            McpRequest.CompleteAndRemove(World, playerEntity, intent, result);

        private static McpPointerClickResult Failure(in McpEcsPointerEventIntent intent, string reason) =>
            new ()
            {
                Hit = false,
                FailureReason = reason,
                SceneEntityId = intent.TargetEntityId,
            };

        private bool TryDeliver(in McpEcsPointerEventIntent intent, World sceneWorld, uint tick, Entity? pressEntity, out McpPointerClickResult result)
        {
            if (!TryResolveTarget(in intent, sceneWorld, pressEntity, out Entity targetEntity, out result))
                return false;

            bool requireTarget = intent.TargetEntityId >= 0;

            Vector3 aimPoint = intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

            CameraComponent camera = playerCamera.GetCameraComponent(World);
            Vector3 origin = camera.Camera.transform.position;
            Vector3 direction = aimPoint - origin;

            if (direction.sqrMagnitude < AIM_DIRECTION_EPSILON_SQR)
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
            pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(intent.Button, intent.EventType);

            result = new McpPointerClickResult
            {
                Hit = true,
                SceneEntityId = targetEntity.Id,
                CrdtEntityId = crdtEntity.Id,
                HoverText = hoverText,
                HitPoint = hit.point,
                Distance = distance,
                Press = new McpPressHandoff
                {
                    World = sceneWorld,
                    Entity = targetEntity,
                    Tick = tick,
                    Hit = hit,
                    Ray = ray,
                },
            };

            return true;
        }

        /// <summary>
        ///     Delivers the release leg of a click. If the target moved out from under the ray after the press
        ///     (or its distance gate no longer qualifies), the press-frame hit is reused so the entity still
        ///     receives an ordered PetUp, and the divergence is reported via <see cref="McpPointerClickResult.UpRayMissed" />.
        /// </summary>
        private void DeliverRelease(in McpEcsPointerEventIntent intent, in McpPressHandoff press, World sceneWorld, uint tick, out McpPointerClickResult result)
        {
            if (TryDeliver(in intent, sceneWorld, tick, press.Entity, out result))
                return;

            // Fresh delivery failed: fall back to the press-frame hit if the component is still reachable.
            if (sceneWorld.IsAlive(press.Entity) && sceneWorld.TryGet(press.Entity, out PBPointerEvents? pbPointerEvents) && pbPointerEvents != null)
            {
                pbPointerEvents.AppendPointerEventResultsIntent.Initialize(press.Hit, press.Ray);
                pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(intent.Button, intent.EventType);

                result = new McpPointerClickResult
                {
                    Hit = true,
                    SceneEntityId = press.Entity.Id,
                    UpRayMissed = true,
                };

                return;
            }

            // Nothing was delivered: keep the fresh failure reason and flag that only the press landed.
            result.UpRayMissed = true;
        }

        private static bool TryResolveTarget(in McpEcsPointerEventIntent intent, World sceneWorld, Entity? pressEntity, out Entity resolved, out McpPointerClickResult result)
        {
            result = default;
            resolved = Entity.Null;

            // Aim-point mode: the validation raycast picks the entity.
            if (intent.AimPoint.HasValue && intent.TargetEntityId < 0)
                return true;

            // The press already resolved the target; only re-validate that it is still alive.
            if (pressEntity.HasValue)
            {
                if (sceneWorld.IsAlive(pressEntity.Value))
                {
                    resolved = pressEntity.Value;
                    return true;
                }

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

            resolved = found;
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
