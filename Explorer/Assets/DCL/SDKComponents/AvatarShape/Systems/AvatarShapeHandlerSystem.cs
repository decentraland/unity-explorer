using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility.Arch;
using Utility.Types;

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
            if (!isSceneEmote) // normal "urn emote" or "base emote"
            {
                globalWorld.AddOrSet(globalWorldEntity,
                    new CharacterEmoteIntent { EmoteId = emoteId });

                return;
            }

            if (!sceneData.SceneContent.TryGetHash(emoteId, out string hash))
            {
                ReportHub.LogError(ReportCategory.AVATAR,$"Scene emote '{emoteId}' not found in scene assets. Aborting scene emote playing on SDK AvatarShape");
                return;
            }

            if (globalWorld.TryGet(globalWorldEntity, out SDKAvatarShapeEmotePromiseCancellationToken? promiseComponent))
                promiseComponent!.Dispose();

            var newPromiseCancellationTokenComponent = new SDKAvatarShapeEmotePromiseCancellationToken();
            globalWorld.AddOrSet(globalWorldEntity, newPromiseCancellationTokenComponent);

            if (localSceneDevelopment)
            {
                LoadAndTriggerSceneEmote(globalWorldEntity,
                    new GetSceneEmoteFromLocalSceneIntention(
                        sceneData,
                        emoteId,
                        hash,
                        bodyShape,
                        loop: false), // looping scene emotes on SDK AvatarShapes is not supported yet
                    newPromiseCancellationTokenComponent
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
                        bodyShape),
                    newPromiseCancellationTokenComponent
                ).Forget();
            }
        }

        private async UniTaskVoid LoadAndTriggerSceneEmote<TIntention>(Entity globalWorldEntity, TIntention intention, SDKAvatarShapeEmotePromiseCancellationToken promiseComponent)
            where TIntention : struct, IAssetIntention, IEquatable<TIntention>
        {
            CancellationToken ct = promiseComponent.Cts.Token;
            try
            {
                // 1. Create the scene emote loading promise and wait for it
                var promise = AssetPromise<EmotesResolution, TIntention>.Create(globalWorld, intention, PartitionComponent.TOP_PRIORITY);

                Result<AssetPromise<EmotesResolution, TIntention>> promiseResult = await promise.ToUniTaskAsync(globalWorld, cancellationToken: ct).SuppressToResultAsync(ReportCategory.AVATAR);

                if (ct.IsCancellationRequested) return;
                if (!globalWorld.IsAlive(globalWorldEntity) || globalWorldEntity == Entity.Null) return;

                // 2. Finally, add the CharacterEmoteIntent to the avatar once the emote has been loaded
                if (promiseResult.Success)
                {
                    AssetPromise<EmotesResolution, TIntention> resolvedPromise = promiseResult.Value;

                    if (resolvedPromise.Result is { Succeeded: true })
                    {
                        using var consumed = resolvedPromise.Result.Value.Asset.ConsumeEmotes();

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
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (globalWorld.IsAlive(globalWorldEntity)
                    && globalWorldEntity != Entity.Null
                    && globalWorld.TryGet(globalWorldEntity, out SDKAvatarShapeEmotePromiseCancellationToken? currentPromiseComponent)
                    && ReferenceEquals(currentPromiseComponent, promiseComponent))
                {
                    currentPromiseComponent.Dispose();
                    globalWorld.Remove<SDKAvatarShapeEmotePromiseCancellationToken>(globalWorldEntity);
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
            if (globalWorld.TryGet(globalEntity, out SDKAvatarShapeEmotePromiseCancellationToken? promiseCancellationComponent))
                promiseCancellationComponent!.Dispose();

            // Need to remove parenting, since it may unintenionally deleted when
            globalWorld.Get<CharacterTransform>(globalEntity).Transform.SetParent(null);

            // Has to be deferred because many times it happens that the entity is marked for deletion AFTER the
            // AvatarCleanUpSystem.Update() and BEFORE the DestroyEntitiesSystem.Update(), probably has to do with
            // non-synchronicity between global and scene ECS worlds. AvatarCleanUpSystem resets the DeferDeletion.
            globalWorld.Add(globalEntity, new DeleteEntityIntention { DeferDeletion = true });
        }
    }
}
