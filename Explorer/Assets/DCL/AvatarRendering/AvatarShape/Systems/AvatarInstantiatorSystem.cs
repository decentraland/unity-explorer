using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarGhostSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private static readonly HashSet<string> EMPTY_STRING_HASH_SET = new (0);

        private readonly IAvatarMaterialPoolHandler avatarMaterialPoolHandler;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly CustomSkinning skinningStrategy;
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IAttachmentsAssetsCache wearableAssetsCache;
        private readonly IPerformanceBudget memoryBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearable?[] fallbackBodyShape = new IWearable[1];

        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob;
        private readonly FacialFeaturesTextures[] facialFeaturesTexturesByBodyShape;
        private readonly FacialFeaturesTextures[] facialFeaturesTexturesByBodyShapeCopy;

        public AvatarInstantiatorSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget,
            IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            IAttachmentsAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IWearableStorage wearableStorage,
            AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob,
            FacialFeaturesTextures[] facialFeaturesTexturesByBodyShape) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;

            this.wearableAssetsCache = wearableAssetsCache;
            this.skinningStrategy = skinningStrategy;
            this.vertOutBuffer = vertOutBuffer;
            this.memoryBudget = memoryBudget;
            this.avatarMaterialPoolHandler = avatarMaterialPoolHandler;
            computeShaderSkinningPool = computeShaderPool;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.wearableStorage = wearableStorage;
            this.avatarTransformMatrixBatchJob = avatarTransformMatrixBatchJob;
            this.facialFeaturesTexturesByBodyShape = facialFeaturesTexturesByBodyShape;

            facialFeaturesTexturesByBodyShapeCopy = new FacialFeaturesTextures[facialFeaturesTexturesByBodyShape.Length];

            for (var i = 0; i < facialFeaturesTexturesByBodyShapeCopy.Length; i++)
                facialFeaturesTexturesByBodyShapeCopy[i] = new FacialFeaturesTextures(new Dictionary<string, Dictionary<int, Texture>>());
        }

        protected override void OnDispose()
        {
            vertOutBuffer.Dispose();
        }

        protected override void Update(float t)
        {
            InstantiateMainPlayerAvatarFromGhostQuery(World);
            InstantiateNewAvatarFromGhostQuery(World);
            InstantiateMainPlayerAvatarRevealTransitionQuery(World);
            InstantiateNewAvatarRevealTransitionQuery(World);
            ApplyRevealTransitionToWearablesQuery(World);
            InstantiateExistingAvatarQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(AvatarGhostComponent))]
        private void InstantiateNewAvatarFromGhost(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent,
            ref AvatarBase avatarBase, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Hidden) return;
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;
            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";
            avatarGhostComponent.Disable();

            var boneArray = BoneArray.FromOrDefault(avatarBase.AvatarSkinnedMeshRenderer.bones!, GetReportCategory());
            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(boneArray);
            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase, skipDisableGhost: false);

            World.Add(entity, avatarTransformMatrixComponent, skinningComponent);

            avatarBase.RigBuilder.enabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.HEAD_SYNC);
            avatarBase.HandsIKRig.enabled = false;
            avatarBase.FeetIKRig.enabled = false;
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(AvatarGhostComponent))]
        private void InstantiateNewAvatarRevealTransition(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent,
            ref AvatarBase avatarBase, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.RevealTransition) return;
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;
            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            var boneArray = BoneArray.FromOrDefault(avatarBase.AvatarSkinnedMeshRenderer.bones!, GetReportCategory());
            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(boneArray);
            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase, skipDisableGhost: true);

            World.Add(entity, avatarTransformMatrixComponent, skinningComponent);

            ApplyRevealPositionToWearables(ref avatarShapeComponent, ref avatarGhostComponent);

            avatarBase.RigBuilder.enabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.HEAD_SYNC);
            avatarBase.HandsIKRig.enabled = false;
            avatarBase.FeetIKRig.enabled = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(AvatarGhostComponent), typeof(AvatarShapeComponent), typeof(AvatarTransformMatrixComponent))]
        private void ApplyRevealTransitionToWearables(ref AvatarGhostComponent avatarGhostComponent, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.InstantiatedWearables.Count == 0) return;

            var revealVec = new Vector4(0, avatarGhostComponent.RevealPosition, 0, 0);
            var inactiveVec = new Vector4(0, AvatarGhostComponent.REVEAL_INACTIVE_Y, 0, 0);
            int shaderId = AvatarGhostComponent.REVEAL_POSITION_SHADER_ID;

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.material == null) continue;
                    Material mat = renderer.material;
                    mat.SetVector(shaderId, avatarGhostComponent.Phase == AvatarGhostPhase.RevealTransition ? revealVec : inactiveVec);
                }
            }
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarBase), typeof(AvatarGhostComponent))]
        [None(typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent), typeof(DeleteEntityIntention))]
        private void InstantiateMainPlayerAvatarFromGhost(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent,
            ref AvatarBase avatarBase, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.Hidden) return;
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;
            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";
            avatarGhostComponent.Disable();

            var boneArray = BoneArray.FromOrDefault(avatarBase.AvatarSkinnedMeshRenderer.bones!, GetReportCategory());
            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(boneArray);
            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase, skipDisableGhost: false);

            World.Add(entity, avatarTransformMatrixComponent, skinningComponent);

            avatarBase.RigBuilder.enabled = true;
            avatarBase.HandsIKRig.enabled = true;
            avatarBase.FeetIKRig.enabled = true;
            mainPlayerAvatarBaseProxy.SetObject(avatarBase);
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarBase), typeof(AvatarGhostComponent))]
        [None(typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private void InstantiateMainPlayerAvatarRevealTransition(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent,
            ref AvatarBase avatarBase, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarGhostComponent.Phase != AvatarGhostPhase.RevealTransition) return;
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;
            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            var boneArray = BoneArray.FromOrDefault(avatarBase.AvatarSkinnedMeshRenderer.bones!, GetReportCategory());
            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(boneArray);
            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase, skipDisableGhost: true);

            World.Add(entity, avatarTransformMatrixComponent, skinningComponent);

            ApplyRevealPositionToWearables(ref avatarShapeComponent, ref avatarGhostComponent);

            avatarBase.RigBuilder.enabled = true;
            avatarBase.HandsIKRig.enabled = true;
            avatarBase.FeetIKRig.enabled = true;
            mainPlayerAvatarBaseProxy.SetObject(avatarBase);
        }

        private static void ApplyRevealPositionToWearables(ref AvatarShapeComponent avatarShapeComponent, ref AvatarGhostComponent avatarGhostComponent)
        {
            if (avatarShapeComponent.InstantiatedWearables.Count == 0) return;
            var revealVec = new Vector4(0, avatarGhostComponent.RevealPosition, 0, 0);
            var inactiveVec = new Vector4(0, AvatarGhostComponent.REVEAL_INACTIVE_Y, 0, 0);
            int shaderId = AvatarGhostComponent.REVEAL_POSITION_SHADER_ID;

            foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
            {
                foreach (Renderer renderer in cachedAttachment.Renderers)
                {
                    if (renderer == null || renderer.material == null) continue;
                    renderer.material.SetVector(shaderId, avatarGhostComponent.Phase == AvatarGhostPhase.RevealTransition ? revealVec : inactiveVec);
                }
            }
        }

        [Query]
        [All(typeof(CharacterTransform))]
        [None(typeof(DeleteEntityIntention))]
        private void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarCustomSkinningComponent skinningComponent,
            ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            ReleaseAvatar.Execute(vertOutBuffer, wearableAssetsCache, avatarMaterialPoolHandler,
                computeShaderSkinningPool, avatarShapeComponent, ref skinningComponent,
                ref avatarTransformMatrixComponent, avatarTransformMatrixBatchJob);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            in WearablesLoadResult wearablesResult,
            AvatarBase avatarBase,
            bool skipDisableGhost = false)
        {
            GetWearablesByPointersIntention wearableIntention = avatarShapeComponent.WearablePromise.LoadingIntention;

            HashSet<string> wearablesToHide = wearablesResult.Succeeded ? wearablesResult.Asset.HiddenCategories : EMPTY_STRING_HASH_SET;
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            GameObject? bodyShape = null;

            IList<IWearable> visibleWearables;

            if (wearablesResult.Succeeded)
                visibleWearables = wearablesResult.Asset.Wearables;
            else
            {
                // We need at least a body shape to be able to instantiate an avatar
                // In case the wearable result failed, fallback into a male body shape to get something visible so the flow doesnt get broken
                if (fallbackBodyShape[0] == null)

                    // Could be a very rare case on which the body shape is not available. This case will make the flow fail
                    if (wearableStorage.TryGetElement(BodyShape.MALE, out IWearable maleBody))
                        fallbackBodyShape[0] = maleBody;

                visibleWearables = fallbackBodyShape!;
            }

            // Restore the original facial feature textures
            facialFeaturesTexturesByBodyShape[avatarShapeComponent.BodyShape]
               .CopyInto(ref facialFeaturesTexturesByBodyShapeCopy[avatarShapeComponent.BodyShape]);

            // Use a copy of the textures so it can be modified during the skinned mesh setup
            FacialFeaturesTextures facialFeatureTextures = facialFeaturesTexturesByBodyShapeCopy[avatarShapeComponent.BodyShape];

            var attachPoint = avatarBase.transform;

            for (var i = 0; i < visibleWearables.Count; i++)
            {
                IWearable resultWearable = visibleWearables[i];

                GameObject? instance = resultWearable.AppendToAvatar(wearableAssetsCache, usedCategories, ref facialFeatureTextures, ref avatarShapeComponent, attachPoint);

                if (resultWearable.Type == WearableType.BodyShape)
                    bodyShape = instance;
            }

            WearableComponentsUtils.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                computeShaderSkinningPool.Get(), avatarMaterialPoolHandler, avatarShapeComponent, facialFeatureTextures);

            if (!avatarShapeComponent.IsVisible)
                foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
                foreach (var renderer in cachedAttachment.Renderers)
                    renderer.enabled = false;

            skinningComponent.SetVertOutRegion(vertOutBuffer.Rent(skinningComponent.VertCount));
            avatarBase.gameObject.SetActive(true);

            if (!skipDisableGhost && avatarBase.GhostRenderer != null)
                avatarBase.GhostRenderer.SetActive(false);
            avatarBase.UpdateHeadWearableOffset(skinningComponent.LocalBounds, wearableIntention); // Update cached head wearable offset for nametag positioning

            avatarShapeComponent.CreateOutlineCompatibilityList();
            wearableIntention.Dispose();

            if (wearablesResult.Succeeded)
                wearablesResult.Asset.Dispose();

            avatarShapeComponent.IsDirty = false;

            return skinningComponent;
        }

        private bool ReadyToInstantiateNewAvatar(ref AvatarShapeComponent avatarShapeComponent) =>
            avatarShapeComponent.IsDirty && instantiationFrameTimeBudget.TrySpendBudget() && memoryBudget.TrySpendBudget();
    }
}
