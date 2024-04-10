using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly Material hoverMaterial;
        private readonly Material hoverOorMaterial;
        private readonly IEventSystem eventSystem;
        private readonly IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;
        private readonly Dictionary<EntityReference, Dictionary<EntityReference, Material[]>> originalMaterialsByEntity = new ();
        private readonly Dictionary<EntityReference, Material> materialOnUseByEntity = new ();

        internal ProcessPointerEventsSystem(World world,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            Material hoverMaterial,
            Material hoverOorMaterial,
            IEventSystem eventSystem) : base(world)
        {
            this.sdkInputActionsMap = sdkInputActionsMap;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.hoverMaterial = hoverMaterial;
            this.hoverOorMaterial = hoverOorMaterial;
            this.eventSystem = eventSystem;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
        }

        private bool TryGetPreviousEntityInfo(in HoverStateComponent stateComponent, out GlobalColliderEntityInfo globalColliderEntityInfo)
        {
            if (!stateComponent.LastHitCollider) // collider was destroyed, nothing to do
            {
                globalColliderEntityInfo = default(GlobalColliderEntityInfo);
                return false;
            }

            return entityCollidersGlobalCache.TryGetEntity(stateComponent.LastHitCollider, out globalColliderEntityInfo); // scene was destroyed, collider was returned to the pool, nothing to do
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResult raycastResult, ref HoverFeedbackComponent hoverFeedbackComponent,
            ref HoverStateComponent hoverStateComponent)
        {
            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Tooltips.Clear();

            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderEntityInfo previousEntityInfo);
            hoverStateComponent.LastHitCollider = null;
            hoverStateComponent.HasCollider = false;
            hoverStateComponent.IsHoverOver = false;
            hoverStateComponent.IsAtDistance = false;

            bool canHover = !eventSystem.IsPointerOverGameObject();

            if (raycastResult.IsValidHit && canHover)
            {
                GlobalColliderEntityInfo entityInfo = raycastResult.EntityInfo.Value;

                InteractionInputUtils.AnyInputInfo anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

                // External world access should be always synchronized (Global World calls into Scene World)
                using (entityInfo.EcsExecutor.Sync.GetScope())
                {
                    World world = entityInfo.EcsExecutor.World;
                    EntityReference entityRef = entityInfo.ColliderEntityInfo.EntityReference;

                    // Entity should be alive and contain PBPointerEvents component to be qualified
                    if (entityRef.IsAlive(world) && world.TryGet(entityRef, out PBPointerEvents pbPointerEvents))
                    {
                        hoverStateComponent.LastHitCollider = raycastResult.UnityRaycastHit.collider;
                        hoverStateComponent.HasCollider = true;

                        bool newEntityWasHovered = !candidateForHoverLeaveIsValid
                                                   || (previousEntityInfo.EcsExecutor.World != world && previousEntityInfo.ColliderEntityInfo.EntityReference != entityRef);

                        // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                        if (candidateForHoverLeaveIsValid && !newEntityWasHovered)
                            candidateForHoverLeaveIsValid = false;

                        pbPointerEvents.AppendPointerEventResultsIntent.Initialize(raycastResult.UnityRaycastHit, raycastResult.OriginRay);

                        bool isAtDistance = SetupPointerEvents(raycastResult, ref hoverFeedbackComponent, pbPointerEvents, anyInputInfo, newEntityWasHovered);

                        hoverStateComponent.IsAtDistance = isAtDistance;
                        Material materialToUse = isAtDistance ? hoverMaterial : hoverOorMaterial;

                        if (!materialOnUseByEntity.ContainsKey(entityRef))
                        {
                            materialOnUseByEntity.TryAdd(entityRef, materialToUse);
                            originalMaterialsByEntity.TryAdd(entityRef, new Dictionary<EntityReference, Material[]>());

                            TryAddHoverMaterialToComponentSiblings(materialToUse, world, entityRef);
                        }
                        else
                        {
                            if (materialOnUseByEntity[entityRef] != materialToUse)
                            {
                                // remove material, its going to be properly added next frame
                                TryRemoveHoverMaterials(entityInfo);
                            }
                        }
                    }
                }
            }

            if (candidateForHoverLeaveIsValid)
            {
                hoverStateComponent.IsHoverOver = true;

                TryRemoveHoverMaterials(previousEntityInfo);

                HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousEntityInfo);
            }
        }

        private void TryRemoveHoverMaterials(GlobalColliderEntityInfo entityInfo)
        {
            World world = entityInfo.EcsExecutor.World;
            EntityReference entity = entityInfo.ColliderEntityInfo.EntityReference;

            if (materialOnUseByEntity.ContainsKey(entity))
            {
                using (entityInfo.EcsExecutor.Sync.GetScope())
                {
                    if (entity.IsAlive(world)) { TryRemoveHoverMaterialFromComponentSiblings(world, entity); }
                }

                materialOnUseByEntity.Remove(entity);
            }
        }

        private void TryRemoveHoverMaterialFromComponentSiblings(World world, EntityReference entityRef)
        {
            if (!world.TryGet(entityRef, out TransformComponent transformComponent)) return;
            if (!world.TryGet(transformComponent.Parent, out TransformComponent parentTransform)) return;

            Dictionary<EntityReference, Material[]> materialDict = originalMaterialsByEntity[entityRef];

            foreach (EntityReference brother in parentTransform.Children)
            {
                if (!world.TryGet(brother, out PrimitiveMeshRendererComponent primitiveMeshRendererComponent))
                    continue;

                if (!materialDict.ContainsKey(brother)) continue;

                primitiveMeshRendererComponent.MeshRenderer.sharedMaterials = materialDict[brother];
                materialDict.Remove(brother);
            }
        }

        private void TryAddHoverMaterialToComponentSiblings(Material targetMaterial, World world, EntityReference entityRef)
        {
            if (!world.TryGet(entityRef, out TransformComponent transformComponent)) return;
            if (!world.TryGet(transformComponent.Parent, out TransformComponent parentTransform)) return;

            Dictionary<EntityReference, Material[]> materialDict = originalMaterialsByEntity[entityRef];

            foreach (EntityReference brother in parentTransform.Children)
            {
                // TODO: we should support other rendereables like gltf
                if (!world.TryGet(brother, out PrimitiveMeshRendererComponent primitiveMeshRendererComponent))
                    continue;

                if (materialDict.ContainsKey(brother))
                    continue;

                List<Material> materials = ListPool<Material>.Get();
                MeshRenderer renderer = primitiveMeshRendererComponent.MeshRenderer;
                materialDict.Add(brother, renderer.sharedMaterials);

                // override materials
                renderer.GetMaterials(materials);
                materials.Add(targetMaterial);
                renderer.SetMaterials(materials);

                ListPool<Material>.Release(materials);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetupPointerEvents(PlayerOriginRaycastResult raycastResult,
            ref HoverFeedbackComponent hoverFeedbackComponent, PBPointerEvents pbPointerEvents, InteractionInputUtils.AnyInputInfo anyInputInfo,
            bool newEntityWasHovered)
        {
            var isAtDistance = false;

            for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                info.PrepareDefaultValues();

                if (!InteractionInputUtils.IsQualifiedByDistance(raycastResult, info)) continue;
                isAtDistance = true;

                // Check Input for validity
                InteractionInputUtils.TryAppendButtonLikeInput(sdkInputActionsMap, pointerEvent, i,
                    ref pbPointerEvents.AppendPointerEventResultsIntent, anyInputInfo);

                // Try Append Hover Input
                if (newEntityWasHovered)
                    InteractionInputUtils.TryAppendHoverInput(ref pbPointerEvents.AppendPointerEventResultsIntent, PointerEventType.PetHoverEnter, pointerEvent, i);

                // Try Append Hover Feedback
                HoverFeedbackUtils.TryAppendHoverFeedback(sdkInputActionsMap, pointerEvent,
                    ref hoverFeedbackComponent, anyInputInfo.AnyButtonIsPressed);
            }

            return isAtDistance;
        }
    }
}
