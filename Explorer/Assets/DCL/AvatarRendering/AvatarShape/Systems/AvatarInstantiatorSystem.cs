using System;
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
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using System.Linq;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Export;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;
using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private static readonly HashSet<string> EMPTY_STRING_HASH_SET = new (0);

        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
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
            IComponentPool<AvatarBase> avatarPoolRegistry, IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            IAttachmentsAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IWearableStorage wearableStorage,
            AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob,
            FacialFeaturesTextures[] facialFeaturesTexturesByBodyShape) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.avatarPoolRegistry = avatarPoolRegistry;

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
            InstantiateMainPlayerAvatarQuery(World);
            InstantiateNewAvatarQuery(World);
            InstantiateExistingAvatarQuery(World);
            InstantiateExportAvatarQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent), typeof(DeleteEntityIntention))]
        private AvatarBase? InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return null;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return null;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            if (transformComponent.Transform != null)
            {
                avatarTransform.SetParent(transformComponent.Transform, false);

                using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

                for (var index = 0; index < children.Value.Count; index++)
                {
                    Transform child = children.Value[index];

                    if (child != null) { child.gameObject.layer = transformComponent.Transform.gameObject.layer; }
                }
            }

            avatarTransform.ResetLocalTRS();

            var boneArray = BoneArray.FromOrDefault(avatarBase.AvatarSkinnedMeshRenderer.bones!, GetReportCategory());
            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(boneArray);

            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase);

            World.Add(entity, avatarBase, (IAvatarView)avatarBase, avatarTransformMatrixComponent, skinningComponent);
            return avatarBase;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private void InstantiateMainPlayerAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            var avatarBase = InstantiateNewAvatar(entity, ref avatarShapeComponent, ref transformComponent);

            if (avatarBase != null)
            {
                avatarBase.RigBuilder.enabled = true;
                mainPlayerAvatarBaseProxy.SetObject(avatarBase);
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
            AvatarBase avatarBase)
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
            
            avatarShapeComponent.FacialFeatureMainTexturesForExport = new Dictionary<string, Texture>();
            avatarShapeComponent.FacialFeatureMaskTexturesForExport = new Dictionary<string, Texture>();

            foreach (var category in facialFeatureTextures.Value.Keys)
            {
                var textureDict = facialFeatureTextures.Value[category];
    
                if (textureDict.TryGetValue(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE, out var mainTex) && mainTex != null)
                    avatarShapeComponent.FacialFeatureMainTexturesForExport[category] = mainTex;
    
                if (textureDict.TryGetValue(TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID, out var maskTex) && maskTex != null)
                    avatarShapeComponent.FacialFeatureMaskTexturesForExport[category] = maskTex;
            }

            WearableComponentsUtils.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                computeShaderSkinningPool.Get(), avatarMaterialPoolHandler, avatarShapeComponent, facialFeatureTextures);

            if (!avatarShapeComponent.IsVisible)
                foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
                foreach (var renderer in cachedAttachment.Renderers)
                    renderer.enabled = false;

            skinningStrategy.SetVertOutRegion(vertOutBuffer.Rent(skinningComponent.VertCount), ref skinningComponent);
            avatarBase.gameObject.SetActive(true);
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

        [Query]
        [All(typeof(VRMExportIntention))]
        [None(typeof(VRMExportDataComponent))]
        private void InstantiateExportAvatar(
            in Entity entity,
            ref AvatarShapeComponent avatarShapeComponent,
            ref VRMExportIntention exportIntent)
        {
            ref readonly var wearablePromise = ref avatarShapeComponent.WearablePromise;

            if (!wearablePromise.TryConsume(World, out StreamableLoadingResult<WearablesResolution> wearablesResult))
                return;

            if (!wearablesResult.Succeeded)
            {
                ReportHub.LogError(GetReportCategory(), "VRM Export: Failed to load wearables");
                World.Destroy(entity);
                return;
            }

            try
            {
                InstantiateExportAvatarInternal(entity, ref avatarShapeComponent, ref exportIntent, wearablesResult);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, GetReportCategory());
                World.Destroy(entity);
            }
        }

        private void InstantiateExportAvatarInternal(
            Entity entity,
            ref AvatarShapeComponent avatarShapeComponent,
            ref VRMExportIntention exportIntent,
            StreamableLoadingResult<WearablesResolution> wearablesResult)
        {
            // Create temporary avatar base for export
            AvatarBase exportAvatarBase = avatarPoolRegistry.Get();
            exportAvatarBase.name = "VRM_Export_Avatar";
            exportAvatarBase.transform.parent = null;
            exportAvatarBase.transform.localPosition = Vector3.zero;
            exportAvatarBase.gameObject.SetActive(true);

            var attachPoint = exportAvatarBase.transform;

            // Use original textures, export is fast enough that user will not be able to change avatar in a meantime.
            FacialFeaturesTextures facialFeatureTextures = facialFeaturesTexturesByBodyShape[avatarShapeComponent.BodyShape];

            HashSet<string> wearablesToHide = wearablesResult.Asset.HiddenCategories;
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            GameObject bodyShape = null;
            IList<IWearable> visibleWearables = wearablesResult.Asset.Wearables;
            var wearableInfos = new List<WearableExportInfo>();

            for (var i = 0; i < visibleWearables.Count; i++)
            {
                IWearable resultWearable = visibleWearables[i];

                wearableInfos.Add(CreateWearableInfo(resultWearable));
                
                // Handle facial features, just populate textures, don't instantiate
                if (resultWearable.Type == WearableType.FacialFeature)
                {
                    string category = resultWearable.GetCategory();
                    var originalAssets = resultWearable.WearableAssetResults[avatarShapeComponent.BodyShape].Results;

                    if (originalAssets?[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]?.Asset != null)
                    {
                        var mainAsset = originalAssets[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX].Value.Asset;
                        var texturesSet = facialFeatureTextures.Value[category];
                        texturesSet[TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE] = ((AttachmentTextureAsset)mainAsset).Texture;

                        var maskAssetRes = originalAssets[WearablePolymorphicBehaviour.MASK_ASSET_INDEX];
                        if (maskAssetRes is { Asset: not null })
                            texturesSet[TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID] = ((AttachmentTextureAsset)maskAssetRes.Value.Asset).Texture;
                    }

                    continue;
                }

                // Instantiate regular wearables
                GameObject? instance = resultWearable.AppendToAvatar(
                    wearableAssetsCache,
                    usedCategories,
                    ref facialFeatureTextures,
                    ref avatarShapeComponent,
                    attachPoint);

                if (resultWearable.Type == WearableType.BodyShape)
                    bodyShape = instance;
            }

            // Hide body parts
            WearableComponentsUtils.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            // Extracted facial feature textures
            var mainTextures = new Dictionary<string, Texture>();
            var maskTextures = new Dictionary<string, Texture>();

            foreach (var category in facialFeatureTextures.Value.Keys)
            {
                var textureDict = facialFeatureTextures.Value[category];

                if (textureDict.TryGetValue(TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE, out var mainTex) && mainTex != null)
                    mainTextures[category] = mainTex;

                if (textureDict.TryGetValue(TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID, out var maskTex) && maskTex != null)
                    maskTextures[category] = maskTex;
            }

            var instantiatedWearables = avatarShapeComponent.InstantiatedWearables;

            var exportData = new VRMExportDataComponent()
            {
                AvatarBase = exportAvatarBase,
                InstantiatedWearables = instantiatedWearables,
                SkinColor = avatarShapeComponent.SkinColor,
                HairColor = avatarShapeComponent.HairColor,
                EyesColor = avatarShapeComponent.EyesColor,
                FacialFeatureMainTextures = mainTextures,
                FacialFeatureMaskTextures = maskTextures,
                WearableInfos = wearableInfos,
                AuthorName = exportIntent.AuthorName,
                SavePath = exportIntent.SavePath,
                OnFinishedAction = exportIntent.OnFinishedAction,
                
                CleanupAction = () =>
                {
                    // Release avatar back to pool
                    if (exportAvatarBase != null)
                    {
                        exportAvatarBase.gameObject.SetActive(false);
                        avatarPoolRegistry.Release(exportAvatarBase);
                    }
                
                    // Dispose wearables
                    foreach (var attachment in instantiatedWearables)
                        if(attachment.Instance != null)
                            attachment.Dispose();
                
                    instantiatedWearables.Clear();
                }
            };

            // Replace intent with export data component
            World.Remove<VRMExportIntention>(entity);
            World.Add(entity, exportData);

            // Dispose wearables result
            if (wearablesResult.Succeeded)
                wearablesResult.Asset.Dispose();

            ReportHub.Log(GetReportCategory(), $"VRM Export: Avatar instantiated with {avatarShapeComponent.InstantiatedWearables.Count} wearables");
        }
        
        private static WearableExportInfo CreateWearableInfo(IWearable wearable)
        {
            var dto = wearable.DTO;
            return new WearableExportInfo
            {
                Name = !string.IsNullOrEmpty(dto.Metadata.name) ? dto.Metadata.name : dto.Metadata.id,
                Category = wearable.GetCategory(),
                MarketPlaceUrl = wearable.GetMarketplaceLink()
            };
        }
    }
}
