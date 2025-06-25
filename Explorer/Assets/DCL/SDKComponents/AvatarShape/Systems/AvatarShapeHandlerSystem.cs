using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Arch;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using SceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class AvatarShapeHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private readonly IComponentPool<Transform> globalTransformPool;
        private readonly ISceneData sceneData;
        private readonly bool localSceneDevelopment;

        public AvatarShapeHandlerSystem(World world, World globalWorld, IComponentPool<Transform> globalTransformPool,
            ISceneData sceneData, bool localSceneDevelopment) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalTransformPool = globalTransformPool;
            this.sceneData = sceneData;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        protected override void Update(float t)
        {
            // We need to wait until the scene restores its original position (from MordorConstants.SCENE_MORDOR_POSITION)
            // to keep the correct global position on which the avatar should be
            if (!sceneData.SceneLoadingConcluded) return;

            LoadAvatarShapeQuery(World);
            UpdateAvatarShapeQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKAvatarShapeComponent), typeof(DeleteEntityIntention))]
        private void LoadAvatarShape(Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            // We have to create a global transform to hold the CharacterTransform. Using the Transform from the TransformComponent
            // may lead to unexpected consequences, since that one is disposed by the scene, while the avatar lives in the global world
            Transform globalTransform = globalTransformPool.Get();
            globalTransform.SetParent(transformComponent.Transform);
            // We ensure that the avatar's transform initializes on the sdk location if the scene applies any offset
            globalTransform.localPosition = Vector3.zero;
            globalTransform.localRotation = Quaternion.identity;
            globalTransform.localScale = Vector3.one;

            var globalWorldEntity = globalWorld.Create(
                pbAvatarShape, partitionComponent,
                new CharacterTransform(globalTransform),
                new CharacterInterpolationMovementComponent(
                    transformComponent.Transform.position,
                    transformComponent.Transform.position,
                    transformComponent.Transform.rotation),
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent());
            World.Add(entity, new SDKAvatarShapeComponent(globalWorldEntity));

            if (!string.IsNullOrEmpty(pbAvatarShape.ExpressionTriggerId))
                AddCharacterEmoteIntent(globalWorldEntity, pbAvatarShape.ExpressionTriggerId, BodyShape.FromStringSafe(pbAvatarShape.BodyShape));
        }

        [Query]
        private void UpdateAvatarShape(ref PBAvatarShape pbAvatarShape, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            globalWorld.Set(sdkAvatarShapeComponent.globalWorldEntity, pbAvatarShape);

            if (!string.IsNullOrEmpty(pbAvatarShape.ExpressionTriggerId))
                AddCharacterEmoteIntent(sdkAvatarShapeComponent.globalWorldEntity, pbAvatarShape.ExpressionTriggerId, BodyShape.FromStringSafe(pbAvatarShape.BodyShape));
        }

        private void AddCharacterEmoteIntent(Entity globalWorldEntity, string emoteId, BodyShape bodyShape)
        {
            bool isSceneEmote = emoteId.ToLower().EndsWith(".glb");

            if (isSceneEmote)
            {
                if (!sceneData.SceneContent.TryGetHash(emoteId, out string hash))
                    return;

                if (localSceneDevelopment)
                {
                    LoadAndTriggerSceneEmote(globalWorldEntity,
                        new GetSceneEmoteFromLocalSceneIntention(
                            sceneData,
                            emoteId,
                            hash,
                            bodyShape,
                            loop: false) // looping scene emotes on SDK AvatarShapes is not supported yet
                    ).Forget();
                }
                else
                {
                    LoadAndTriggerSceneEmote(globalWorldEntity,
                        new GetSceneEmoteFromRealmIntention(
                            sceneData.SceneEntityDefinition.id!,
                            sceneData.AssetBundleManifest,
                            hash,
                            loop: false, // looping scene emotes on SDK AvatarShapes is not supported yet
                            bodyShape)
                    ).Forget();
                }
            }
            else
            {
                globalWorld.AddOrSet(globalWorldEntity,
                    new CharacterEmoteIntent() { EmoteId = emoteId });
            }
        }

        private async UniTaskVoid LoadAndTriggerSceneEmote<TIntention>(Entity globalWorldEntity, TIntention intention) where TIntention : struct, IAssetIntention, IEquatable<TIntention>
        {
            // 1. Create the scene emote loading promise and wait for it
            var promise = AssetPromise<EmotesResolution, TIntention>.Create(globalWorld, intention, PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(globalWorld);

            if (!globalWorld.IsAlive(globalWorldEntity) || globalWorldEntity == Entity.Null) return;

            // 2. Finally, add the CharacterEmoteIntent to the avatar once the emote has been loaded
            if (promise.Result is { Succeeded: true })
            {
                using var consumed = promise.Result.Value.Asset.ConsumeEmotes();

                if (consumed.Value.Count > 0)
                {
                    URN emoteUrn = consumed.Value[0]!.GetUrn();

                    globalWorld.AddOrSet(globalWorldEntity,
                        new CharacterEmoteIntent
                        {
                            EmoteId = emoteUrn,
                            Spatial = true,
                            TriggerSource = TriggerSource.SCENE,
                        });
                }
            }
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            // If the component is removed at scene-world, the global-world representation should disappear entirely
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);

            World.Remove<SDKAvatarShapeComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, ref SDKAvatarShapeComponent sdkAvatarShapeComponent)
        {
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);
            World.Remove<SDKAvatarShapeComponent>(entity);
        }

        [Query]
        public void FinalizeComponents(ref SDKAvatarShapeComponent sdkAvatarShapeComponent) =>
            MarkGlobalWorldEntityForDeletion(sdkAvatarShapeComponent.globalWorldEntity);

        public void FinalizeComponents(in Query query) =>
            FinalizeComponentsQuery(World);

        public void MarkGlobalWorldEntityForDeletion(Entity globalEntity)
        {
            // Need to remove parenting, since it may unintenionally deleted when
            globalWorld.Get<CharacterTransform>(globalEntity).Transform.SetParent(null);

            // Has to be deferred because many times it happens that the entity is marked for deletion AFTER the
            // AvatarCleanUpSystem.Update() and BEFORE the DestroyEntitiesSystem.Update(), probably has to do with
            // non-synchronicity between global and scene ECS worlds. AvatarCleanUpSystem resets the DeferDeletion.
            globalWorld.Add(globalEntity, new DeleteEntityIntention() { DeferDeletion = true });
        }
    }
}
