using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.FeatureFlags;
using DCL.Nametags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DCL.AvatarRendering;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.ECSComponents;
using DCL.Friends.UserBlocking;
using DCL.Quality;
using ECS.LifeCycle.Systems;
using Runtime.Wearables;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Utility;
using Utility.UIToolkit;
using AvatarFacialAnimationSystem = DCL.AvatarRendering.AvatarShape.AvatarFacialAnimationSystem;
using AvatarCleanUpSystem = DCL.AvatarRendering.AvatarShape.AvatarCleanUpSystem;
using AvatarInstantiatorSystem = DCL.AvatarRendering.AvatarShape.AvatarInstantiatorSystem;
using AvatarLoaderSystem = DCL.AvatarRendering.AvatarShape.AvatarLoaderSystem;
using AvatarShapeVisibilitySystem = DCL.AvatarRendering.AvatarShape.AvatarShapeVisibilitySystem;
using FinishAvatarMatricesCalculationSystem = DCL.AvatarRendering.AvatarShape.FinishAvatarMatricesCalculationSystem;
using MakeVertsOutBufferDefragmentationSystem = DCL.AvatarRendering.AvatarShape.MakeVertsOutBufferDefragmentationSystem;
using Object = UnityEngine.Object;
using StartAvatarMatricesCalculationSystem = DCL.AvatarRendering.AvatarShape.StartAvatarMatricesCalculationSystem;
#if UNITY_EDITOR
using DCL.AvatarAnimation;
#endif

namespace DCL.PluginSystem.Global
{
    public class AvatarPlugin : IDCLGlobalPlugin<AvatarPlugin.AvatarShapeSettings>
    {
        private static readonly int GLOBAL_AVATAR_BUFFER = Shader.PropertyToID("_GlobalAvatarBuffer");

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IPerformanceBudget frameTimeCapBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly IRealmData realmData;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly bool includeBannedUsersFromScene;

        private readonly AttachmentsAssetsCache attachmentsAssetsCache;

        // late init
        private IComponentPool<AvatarBase> avatarPoolRegistry = null!;
        private IAvatarMaterialPoolHandler avatarMaterialPoolHandler = null!;
        private IExtendedObjectPool<ComputeShader> computeShaderPool = null!;

        private readonly NametagsData nametagsData;

        private IComponentPool<Transform> transformPoolRegistry = null!;
        private Transform? poolParent = null;

        private IObjectPool<NametagHolder> nametagHolderPool = null!;
        private TextureArrayContainer textureArrayContainer;

        private AvatarRandomizerAsset avatarRandomizerAsset;

        private readonly TextureArrayContainerFactory textureArrayContainerFactory;
        private readonly IWearableStorage wearableStorage;
        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixJobWrapper;

        private float startFadeDistanceDithering;
        private float endFadeDistanceDithering;
        private ReadOnlyAvatarHighlightData highlightData;

        private FacialFeaturesTextures[] facialFeaturesTextures;
        private Texture2DArray? eyebrowsTextureArray;
        private Texture2DArray? eyeTextureArray;
        private float minBlinkInterval;
        private float maxBlinkInterval;
        private float blinkFrameDuration;

        private Texture2DArray? mouthPhonemeTextureArray;
        private float phonemeDuration;

        private AvatarFaceExpressionDefinition[] faceExpressions = System.Array.Empty<AvatarFaceExpressionDefinition>();
        private readonly AvatarFaceDebugData avatarFaceDebugData = new ();

        public AvatarPlugin(
            IComponentPoolsRegistry poolsRegistry,
            IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameTimeCapBudget,
            IPerformanceBudget memoryBudget,
            IRendererFeaturesCache rendererFeaturesCache,
            IRealmData realmData,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IDebugContainerBuilder debugContainerBuilder,
            CacheCleaner cacheCleaner,
            NametagsData nametagsData,
            TextureArrayContainerFactory textureArrayContainerFactory,
            IWearableStorage wearableStorage,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            bool includeBannedUsersFromScene)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.realmData = realmData;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cacheCleaner = cacheCleaner;
            this.memoryBudget = memoryBudget;
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.nametagsData = nametagsData;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
            this.wearableStorage = wearableStorage;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
            componentPoolsRegistry = poolsRegistry;
            avatarTransformMatrixJobWrapper = new AvatarTransformMatrixJobWrapper();
            attachmentsAssetsCache = new AttachmentsAssetsCache(100, poolsRegistry);

            cacheCleaner.Register(attachmentsAssetsCache);
        }

        public void Dispose()
        {
            attachmentsAssetsCache.Dispose();
            avatarTransformMatrixJobWrapper.Dispose();
            UnityObjectUtils.SafeDestroyGameObject(poolParent);

            if (eyebrowsTextureArray != null)
                Object.Destroy(eyebrowsTextureArray);

            if (eyeTextureArray != null)
                Object.Destroy(eyeTextureArray);

            if (mouthPhonemeTextureArray != null)
                Object.Destroy(mouthPhonemeTextureArray);
        }

        public async UniTask InitializeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            startFadeDistanceDithering = settings.startFadeDistanceDithering;
            endFadeDistanceDithering = settings.endFadeDistanceDithering;
            highlightData = new ReadOnlyAvatarHighlightData(await settings.OutlineSettingsRef.LoadAssetAsync());

            await CreateAvatarBasePoolAsync(settings, ct);
            await CreateNametagPoolAsync(settings, ct);
            await CreateMaterialPoolPrewarmedAsync(settings, ct);
            await CreateComputeShaderPoolPrewarmedAsync(settings, ct);
            facialFeaturesTextures = await CreateDefaultFaceTexturesByBodyShapeAsync(settings, ct);
            eyebrowsTextureArray = await CreateEyebrowsTextureArrayAsync(settings, ct);
            eyeTextureArray = await CreateEyeTextureArrayAsync(settings, ct);
            minBlinkInterval = settings.MinBlinkInterval;
            maxBlinkInterval = settings.MaxBlinkInterval;
            blinkFrameDuration = settings.BlinkFrameDuration;

            mouthPhonemeTextureArray = await CreateMouthPhonemeTextureArrayAsync(settings, ct);
            phonemeDuration = settings.PhonemeDuration;

            faceExpressions = await LoadFaceExpressionsAsync(settings, ct);

            transformPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<Transform>().EnsureNotNull("ReferenceTypePool of type Transform not found in the registry");
            avatarRandomizerAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.AvatarRandomizerSettingsRef, ct)).Value;

            debugContainerBuilder.TryAddWidget("Nametags")
                                ?.AddToggleField("ShowNametags", _ => nametagsData.showNameTags = !nametagsData.showNameTags, nametagsData.showNameTags);

            BuildAvatarFaceDebugWidget();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var vertOutBuffer = new FixedComputeBufferHandler(5_000_000, Unsafe.SizeOf<CustomSkinningVertexInfo>());
            Shader.SetGlobalBuffer(GLOBAL_AVATAR_BUFFER, vertOutBuffer.Buffer);

            var skinningStrategy = new ComputeShaderSkinning();

            AvatarLoaderSystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBAvatarShape>.InjectToWorld(ref builder);

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.AVATAR_HIGHLIGHT))
                AvatarHighlightSystem.InjectToWorld(ref builder, highlightData);

            cacheCleaner.Register(avatarPoolRegistry);
            cacheCleaner.Register(computeShaderPool);

            foreach (var extendedObjectPool in avatarMaterialPoolHandler.GetAllMaterialsPools())
                cacheCleaner.Register(extendedObjectPool.Pool);

            AvatarInstantiatorSystem.InjectToWorld(ref builder, frameTimeCapBudget, memoryBudget, avatarPoolRegistry, avatarMaterialPoolHandler, computeShaderPool, attachmentsAssetsCache, skinningStrategy, vertOutBuffer, mainPlayerAvatarBaseProxy, wearableStorage, avatarTransformMatrixJobWrapper, facialFeaturesTextures);
            MakeVertsOutBufferDefragmentationSystem.InjectToWorld(ref builder, vertOutBuffer, skinningStrategy);
            StartAvatarMatricesCalculationSystem.InjectToWorld(ref builder, avatarTransformMatrixJobWrapper);
            FinishAvatarMatricesCalculationSystem.InjectToWorld(ref builder, skinningStrategy, avatarTransformMatrixJobWrapper);
            AvatarShapeVisibilitySystem.InjectToWorld(ref builder, userBlockingCacheProxy, rendererFeaturesCache, startFadeDistanceDithering, endFadeDistanceDithering, includeBannedUsersFromScene);
            AvatarCleanUpSystem.InjectToWorld(ref builder, frameTimeCapBudget, vertOutBuffer, avatarMaterialPoolHandler, avatarPoolRegistry, computeShaderPool, attachmentsAssetsCache, mainPlayerAvatarBaseProxy, avatarTransformMatrixJobWrapper);

            AvatarFacialAnimationSystem.InjectToWorld(ref builder, eyebrowsTextureArray, eyeTextureArray, minBlinkInterval, maxBlinkInterval, blinkFrameDuration, mouthPhonemeTextureArray, phonemeDuration, avatarFaceDebugData);

            NametagPlacementSystem.InjectToWorld(ref builder, nametagHolderPool, nametagsData);
            NameTagCleanUpSystem.InjectToWorld(ref builder, nametagsData, nametagHolderPool);

            //Debug scripts
            InstantiateRandomAvatarsSystem.InjectToWorld(ref builder, debugContainerBuilder, realmData, transformPoolRegistry, avatarRandomizerAsset);
#if UNITY_EDITOR
            PlayableDirectorUpdatingSystem.InjectToWorld(ref builder);
#endif
        }

        private async UniTask CreateAvatarBasePoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            AvatarBase avatarBasePrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AvatarBase, ct: ct)).Value.EnsureGetComponent<AvatarBase>();

            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity));
            avatarPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<AvatarBase>().EnsureNotNull("ReferenceTypePool of type AvatarBase not found in the registry");
        }

        private async UniTask CreateNametagPoolAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            NametagHolder nametagPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.NametagHolder, ct: ct)).Value;

            var poolRoot = componentPoolsRegistry.RootContainerTransform();
            poolParent = new GameObject("POOL_CONTAINER_NameTags").transform;
            poolParent.parent = poolRoot;

            nametagHolderPool = new ObjectPool<NametagHolder>(
                () =>
                {
                    var nametagHolder = Object.Instantiate(nametagPrefab, Vector3.zero, Quaternion.identity, poolParent);
                    return nametagHolder;
                },
                actionOnRelease: nh =>
                {
                    nh.gameObject.SetActive(false);
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                actionOnGet: nh => nh.gameObject.SetActive(true));
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            Material toonMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.CelShadingMaterial, ct: ct)).Value;
            Material faceFeatureMaterial = (await assetsProvisioner.ProvideMainAssetAsync(settings.FaceFeatureMaterial, ct: ct)).Value;

#if UNITY_EDITOR

            //Avoid generating noise in editor git by creating a copy of the material
            toonMaterial = new Material(toonMaterial);
            faceFeatureMaterial = new Material(faceFeatureMaterial);
#endif

            //Set initial dither properties obtained through settings
            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_START_PARAM_ID, startFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_START_PARAM_ID, startFadeDistanceDithering);

            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_END_PARAM_ID, endFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);

            //Default should be visible
            toonMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);
            faceFeatureMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, startFadeDistanceDithering);

            avatarMaterialPoolHandler = new AvatarMaterialPoolHandler(new List<Material>
            {
                toonMaterial, faceFeatureMaterial,
            }, settings.defaultMaterialCapacity, textureArrayContainerFactory);
        }

        private async UniTask CreateComputeShaderPoolPrewarmedAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<ComputeShader> providedComputeShader = await assetsProvisioner.ProvideMainAssetAsync(settings.ComputeShader, ct: ct);
            computeShaderPool = new ExtendedObjectPool<ComputeShader>(() => Object.Instantiate(providedComputeShader.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.defaultMaterialCapacity);

            for (var i = 0; i < PoolConstants.COMPUTE_SHADER_COUNT; i++)
            {
                ComputeShader prewarmedShader = computeShaderPool.Get()!;
                computeShaderPool.Release(prewarmedShader);
            }
        }

        private async UniTask<FacialFeaturesTextures[]> CreateDefaultFaceTexturesByBodyShapeAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            var maleMouthTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleMouthTexture, ct: ct)).Value;
            var maleEyebrowsTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleEyebrowsTexture, ct: ct)).Value;
            var maleEyesTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultMaleEyesTexture, ct: ct)).Value;
            var femaleMouthTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleMouthTexture, ct: ct)).Value;
            var femaleEyebrowsTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleEyebrowsTexture, ct: ct)).Value;
            var femaleEyesTexture = (await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultFemaleEyesTexture, ct: ct)).Value;

            return new FacialFeaturesTextures[]
            {
                new (new Dictionary<string, Dictionary<int, Texture>>
                {
                    [WearableCategories.Categories.EYES] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleEyesTexture },
                    [WearableCategories.Categories.MOUTH] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleMouthTexture },
                    [WearableCategories.Categories.EYEBROWS] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = maleEyebrowsTexture },
                }),
                new (new Dictionary<string, Dictionary<int, Texture>>
                {
                    [WearableCategories.Categories.EYES] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleEyesTexture },
                    [WearableCategories.Categories.MOUTH] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleMouthTexture },
                    [WearableCategories.Categories.EYEBROWS] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = femaleEyebrowsTexture },
                }),
            };
        }

        /// <summary>
        ///     Loads the face expression definitions from the configured ScriptableObject asset.
        ///     Returns an empty array when no asset is configured.
        /// </summary>
        private async UniTask<AvatarFaceExpressionDefinition[]> LoadFaceExpressionsAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            if (settings.ExpressionConfig == null || string.IsNullOrEmpty(settings.ExpressionConfig.AssetGUID))
                return System.Array.Empty<AvatarFaceExpressionDefinition>();

            var config = (await assetsProvisioner.ProvideMainAssetAsync(settings.ExpressionConfig, ct)).Value;
            return config.Expressions ?? System.Array.Empty<AvatarFaceExpressionDefinition>();
        }

        /// <summary>
        ///     Builds the "Avatar Face" debug widget that lets developers transition between
        ///     named expressions and manually override individual eyebrows, eyes, and mouth indices.
        /// </summary>
        private void BuildAvatarFaceDebugWidget()
        {
            DebugWidgetBuilder? widget = debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.AVATAR_FACE);

            if (widget == null)
                return;

            bool updatingFromExpression = false;

            // Individual-override bindings (sliders 0-15).
            var eyebrowsBinding = new ElementBinding<int>(0, evt =>
            {
                if (updatingFromExpression) return;
                avatarFaceDebugData.EyebrowsIndex = evt.newValue;
                avatarFaceDebugData.IsDirty = true;
            });

            var eyesBinding = new ElementBinding<int>(0, evt =>
            {
                if (updatingFromExpression) return;
                avatarFaceDebugData.EyesIndex = evt.newValue;
                avatarFaceDebugData.IsDirty = true;
            });

            var mouthBinding = new ElementBinding<int>(0, evt =>
            {
                if (updatingFromExpression) return;
                avatarFaceDebugData.MouthIndex = evt.newValue;
                avatarFaceDebugData.IsDirty = true;
            });

            // Named expression selector — cycles through faceExpressions and updates the sliders.
            if (faceExpressions.Length > 0)
            {
                var expressionNameBinding = new ElementBinding<string>(faceExpressions[0].Name);
                int expressionCount = faceExpressions.Length;

                var expressionIndexBinding = new ElementBinding<int>(0, evt =>
                {
                    int idx = Mathf.Clamp(evt.newValue, 0, expressionCount - 1);
                    AvatarFaceExpressionDefinition def = faceExpressions[idx];

                    updatingFromExpression = true;
                    expressionNameBinding.SetAndUpdate(def.Name);
                    eyebrowsBinding.SetAndUpdate(def.EyebrowsIndex);
                    eyesBinding.SetAndUpdate(def.EyesIndex);
                    mouthBinding.SetAndUpdate(def.MouthIndex);
                    updatingFromExpression = false;

                    avatarFaceDebugData.EyebrowsIndex = def.EyebrowsIndex;
                    avatarFaceDebugData.EyesIndex = def.EyesIndex;
                    avatarFaceDebugData.MouthIndex = def.MouthIndex;
                    avatarFaceDebugData.IsDirty = true;
                });

                widget.AddCustomMarker(expressionNameBinding)
                      .AddIntSliderField("Expression", expressionIndexBinding, 0, expressionCount - 1);
            }

            widget.AddIntSliderField("Eyebrows", eyebrowsBinding, 0, 15)
                  .AddIntSliderField("Eyes", eyesBinding, 0, 15)
                  .AddIntSliderField("Mouth", mouthBinding, 0, 15);
        }

        /// <summary>
        ///     Slices the 1024×1024 eyebrows atlas into 16 individual 256×256 Texture2DArray slices.
        ///     Uses Graphics.Blit + ReadPixels so it works regardless of whether the atlas is compressed.
        /// </summary>
        private async UniTask<Texture2DArray?> CreateEyebrowsTextureArrayAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            if (settings.EyebrowsAtlasTexture == null || string.IsNullOrEmpty(settings.EyebrowsAtlasTexture.AssetGUID))
                return null;

            var atlasTex = (Texture2D)(await assetsProvisioner.ProvideMainAssetAsync(settings.EyebrowsAtlasTexture, ct: ct)).Value;

            return SliceAtlasIntoTextureArray(atlasTex);
        }

        /// <summary>
        ///     Slices the 1024×1024 eye atlas into 16 individual 256×256 Texture2DArray slices,
        ///     one per eye state.
        /// </summary>
        private async UniTask<Texture2DArray?> CreateEyeTextureArrayAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            if (settings.EyesAtlasTexture == null || string.IsNullOrEmpty(settings.EyesAtlasTexture.AssetGUID))
                return null;

            var atlasTex = (Texture2D)(await assetsProvisioner.ProvideMainAssetAsync(settings.EyesAtlasTexture, ct: ct)).Value;
            return SliceAtlasIntoTextureArray(atlasTex);
        }

        /// <summary>
        ///     Slices the 1024×1024 mouth atlas into 16 individual 256×256 Texture2DArray slices,
        ///     one per phoneme / expression entry.
        /// </summary>
        private async UniTask<Texture2DArray?> CreateMouthPhonemeTextureArrayAsync(AvatarShapeSettings settings, CancellationToken ct)
        {
            if (settings.MouthAtlasTexture == null || string.IsNullOrEmpty(settings.MouthAtlasTexture.AssetGUID))
                return null;

            var atlasTex = (Texture2D)(await assetsProvisioner.ProvideMainAssetAsync(settings.MouthAtlasTexture, ct: ct)).Value;
            return SliceAtlasIntoTextureArray(atlasTex);
        }

        /// <summary>
        ///     Slices a 1024×1024 atlas into 16 individual 256×256 Texture2DArray slices (4×4 grid).
        ///     Uses Graphics.Blit + ReadPixels so it works regardless of whether the atlas is compressed
        ///     (Graphics.CopyTexture sub-region fails for compressed formats).
        /// </summary>
        private static Texture2DArray SliceAtlasIntoTextureArray(Texture2D atlasTex)
        {
            const int cellSize = 256;
            const int cols = 4;
            const int rows = 4;
            const int sliceCount = rows * cols; // 16

            var array = new Texture2DArray(cellSize, cellSize, sliceCount, TextureFormat.RGBA32, false, false);
            RenderTexture rt = RenderTexture.GetTemporary(cellSize, cellSize, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(cellSize, cellSize, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                for (var i = 0; i < sliceCount; i++)
                {
                    int row = i / cols; // visual row: 0 = top of atlas image
                    int col = i % cols;

                    // UV bottom-left of this cell (Unity UV origin is bottom-left).
                    var scale  = new Vector2(1f / cols, 1f / rows);
                    var offset = new Vector2(col / (float)cols, (rows - 1 - row) / (float)rows);

                    Graphics.Blit(atlasTex, rt, scale, offset);

                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, cellSize, cellSize), 0, 0, false);
                    readback.Apply(false);
                    RenderTexture.active = previousActive;

                    Graphics.CopyTexture(readback, 0, 0, array, i, 0);
                }

                array.Apply(false, true);
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(readback);
            }

            return array;
        }

        [Serializable]
        public class AvatarShapeSettings : IDCLPluginSettings
        {
            [field: Header(nameof(AvatarPlugin) + "." + nameof(AvatarShapeSettings))]
            [field: Space]
            [field: SerializeField]
            private AssetReferenceGameObject? avatarBase;

            [field: SerializeField]
            private AssetReferenceMaterial? celShadingMaterial;

            [field: SerializeField]
            private AssetReferenceMaterial? faceFeatureMaterial;

            [field: SerializeField]
            public float startFadeDistanceDithering = 2;

            [field: SerializeField]
            public float endFadeDistanceDithering = 0.8f;

            [field: SerializeField]
            public int defaultMaterialCapacity = 100;

            [field: SerializeField]
            public AssetReferenceComputeShader computeShader;

            [field: SerializeField]
            public StaticSettings.AvatarRandomizerSettingsRef AvatarRandomizerSettingsRef { get; set; }

            [field: SerializeField]
            public NametagHolderRef NametagHolder { get; set; }

            [field: SerializeField]
            public StaticSettings.AvatarOutlineSettingsRef OutlineSettingsRef;

            public AssetReferenceGameObject AvatarBase => avatarBase.EnsureNotNull();

            public AssetReferenceComputeShader ComputeShader => computeShader.EnsureNotNull();

            public AssetReferenceMaterial CelShadingMaterial => celShadingMaterial.EnsureNotNull();

            public AssetReferenceMaterial FaceFeatureMaterial => faceFeatureMaterial.EnsureNotNull();

            public AssetReferenceT<Texture> DefaultMaleMouthTexture;
            public AssetReferenceT<Texture> DefaultMaleEyesTexture;
            public AssetReferenceT<Texture> DefaultMaleEyebrowsTexture;
            public AssetReferenceT<Texture> DefaultFemaleMouthTexture;
            public AssetReferenceT<Texture> DefaultFemaleEyesTexture;
            public AssetReferenceT<Texture> DefaultFemaleEyebrowsTexture;

            [Header("Eyebrows Atlas")]
            public AssetReferenceT<Texture2D> EyebrowsAtlasTexture;

            [Header("Blink")]
            public AssetReferenceT<Texture2D> EyesAtlasTexture;
            public float MinBlinkInterval = 2.0f;
            public float MaxBlinkInterval = 8.0f;
            public float BlinkFrameDuration = 0.05f;

            [Header("Mouth Phoneme Animation")]
            public AssetReferenceT<Texture2D> MouthAtlasTexture;
            public float PhonemeDuration = 0.08f;

            [Header("Face Expressions")]
            public AssetReferenceT<AvatarFaceExpressionConfig> ExpressionConfig;

            [Serializable]
            public class NametagsDataRef : AssetReferenceT<NametagsData>
            {
                public NametagsDataRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class NametagHolderRef : ComponentReference<NametagHolder>
            {
                public NametagHolderRef(string guid) : base(guid) { }
            }
        }
    }
}
