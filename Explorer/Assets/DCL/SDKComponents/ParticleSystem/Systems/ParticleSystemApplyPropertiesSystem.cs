using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem.Components;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(ParticleSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemLifecycleSystem))]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemApplyPropertiesSystem : BaseUnityLoopSystem
    {
        private const int GET_TEXTURE_MAX_ATTEMPT_COUNT = 6;

        private static readonly int SRC_BLEND_ID = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND_ID = Shader.PropertyToID("_DstBlend");
        private static readonly int BLEND_MODE_ID = Shader.PropertyToID("_Blend");
        private static readonly int SURFACE_ID = Shader.PropertyToID("_Surface");
        private static readonly int ZWRITE_ID = Shader.PropertyToID("_ZWrite");

        private readonly ISceneData sceneData;
        private readonly IPartitionComponent partitionComponent;
        private readonly IObjectPool<Material> materialPool;

        internal ParticleSystemApplyPropertiesSystem(World world, ISceneData sceneData, IPartitionComponent partitionComponent, IObjectPool<Material> materialPool) : base(world)
        {
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.materialPool = materialPool;
        }

        protected override void Update(float t)
        {
            ApplyParticleSystemPropertiesQuery(World);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        private void ApplyParticleSystemProperties(ref PBParticleSystem particleSystemData, ref ParticleSystemComponent component)
        {
            if (!particleSystemData.IsDirty) return;

            var particleSystem = component.ParticleSystemInstance;

            ApplyMain(particleSystemData, particleSystem);
            ApplyEmission(particleSystemData, particleSystem, ref component);
            ApplyShape(particleSystemData, particleSystem);
            ApplySizeOverLifetime(particleSystemData, particleSystem, ref component);
            ApplyRotationOverLifetime(particleSystemData, particleSystem);
            ApplyColorOverLifetime(particleSystemData, particleSystem, ref component);
            ApplyForceOverLifetime(particleSystemData, particleSystem);
            ApplyLimitVelocityOverLifetime(particleSystemData, particleSystem);
            ApplySpriteSheet(particleSystemData, particleSystem, ref component);
            ApplyRenderer(particleSystemData, ref component);
        }

        private static void ApplyMain(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var mainModule = particleSystem.main;

            mainModule.loop = particleSystemData.GetLoop();
            mainModule.prewarm = mainModule.loop && particleSystemData.GetPrewarm();

            mainModule.simulationSpace = particleSystemData.GetSimulationSpace() == PBParticleSystem.Types.SimulationSpace.PssWorld
                ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;

            mainModule.startLifetime = particleSystemData.GetLifetime();
            mainModule.maxParticles = (int)particleSystemData.GetMaxParticles();
            mainModule.gravityModifier = particleSystemData.GetGravity();

            // Initial size: random between start and end
            mainModule.startSize = particleSystemData.GetInitialSize();

            // Initial rotation: quaternion -> Euler, apply as 3D start rotation
            mainModule.startRotation3D = particleSystemData.InitialRotation != null;
            Vector3 initialEulerRotation = particleSystemData.GetInitialRotation();
            mainModule.startRotationX = initialEulerRotation.x;
            mainModule.startRotationY = initialEulerRotation.y;
            mainModule.startRotationZ = initialEulerRotation.z;

            // Initial color: random between start and end
            mainModule.startColor = particleSystemData.GetInitialColor();

            // Initial speed: random between start and end
            mainModule.startSpeed = particleSystemData.GetInitialVelocitySpeed();
        }

        private static void ApplyEmission(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem, ref ParticleSystemComponent component)
        {
            var emissionModule = particleSystem.emission;
            emissionModule.enabled = particleSystemData.GetActive();
            emissionModule.rateOverTime = particleSystemData.GetRate();

            int burstCount = particleSystemData.Bursts.Count;

            if (burstCount == 0)
            {
                emissionModule.burstCount = 0;
                return;
            }

            if (component.CachedBursts == null || component.CachedBursts.Length < burstCount)
                component.CachedBursts = new UnityEngine.ParticleSystem.Burst[burstCount];

            for (int i = 0; i < burstCount; i++)
            {
                var protoBurst = particleSystemData.Bursts[i];
                short count = (short)protoBurst.Count;
                component.CachedBursts[i] = new UnityEngine.ParticleSystem.Burst(
                    protoBurst.Time,
                    count,
                    count,
                    protoBurst.GetCycles(),
                    protoBurst.GetInterval()
                );
                component.CachedBursts[i].probability = protoBurst.GetProbability();
            }

            emissionModule.SetBursts(component.CachedBursts, burstCount);
            emissionModule.burstCount = burstCount;
        }

        private static void ApplyShape(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var shapeModule = particleSystem.shape;

            shapeModule.scale = Vector3.one;
            switch (particleSystemData.ShapeCase)
            {
                case PBParticleSystem.ShapeOneofCase.None: // Default: point emitter
                case PBParticleSystem.ShapeOneofCase.Point:
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radius = 0f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Sphere:
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radius = particleSystemData.Sphere.GetRadius();
                    break;

                case PBParticleSystem.ShapeOneofCase.Cone:
                    shapeModule.shapeType = ParticleSystemShapeType.Cone;
                    shapeModule.angle = particleSystemData.Cone.GetAngle();
                    shapeModule.radius = particleSystemData.Cone.GetRadius();
                    break;

                case PBParticleSystem.ShapeOneofCase.Box:
                    shapeModule.shapeType = ParticleSystemShapeType.Box;
                    if (particleSystemData.Box.Size != null)
                        shapeModule.scale = new Vector3(particleSystemData.Box.Size.X, particleSystemData.Box.Size.Y, particleSystemData.Box.Size.Z);
                    break;
            }

            shapeModule.alignToDirection = particleSystemData.GetFaceTravelDirection();
        }

        private static void ApplySizeOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem, ref ParticleSystemComponent component)
        {
            var sizeOverLifetimeModule = particleSystem.sizeOverLifetime;

            if (particleSystemData.SizeOverTime == null)
            {
                sizeOverLifetimeModule.enabled = false;
                return;
            }

            sizeOverLifetimeModule.enabled = true;
            sizeOverLifetimeModule.separateAxes = false;

            // Linear lerp from start to end over particle lifetime using a two-key curve
            sizeOverLifetimeModule.size = BuildLinearCurve(particleSystemData.SizeOverTime.Start, particleSystemData.SizeOverTime.End, ref component.CachedCurve);
        }

        private static void ApplyRotationOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var rotationOverLifetimeModule = particleSystem.rotationOverLifetime;

            if (particleSystemData.RotationOverTime == null)
            {
                rotationOverLifetimeModule.enabled = false;
                return;
            }

            rotationOverLifetimeModule.enabled = true;
            rotationOverLifetimeModule.separateAxes = true;

            Vector3 rotationOverTimeEuler = particleSystemData.GetRotationOverTime();
            rotationOverLifetimeModule.x = new UnityEngine.ParticleSystem.MinMaxCurve(rotationOverTimeEuler.x);
            rotationOverLifetimeModule.y = new UnityEngine.ParticleSystem.MinMaxCurve(rotationOverTimeEuler.y);
            rotationOverLifetimeModule.z = new UnityEngine.ParticleSystem.MinMaxCurve(rotationOverTimeEuler.z);
        }

        private static void ApplyColorOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem, ref ParticleSystemComponent component)
        {
            var colorOverLifetimeModule = particleSystem.colorOverLifetime;

            if (particleSystemData.ColorOverTime == null)
            {
                colorOverLifetimeModule.enabled = false;
                return;
            }

            colorOverLifetimeModule.enabled = true;
            component.UpdateColorOverLifetimeCache(particleSystemData.ColorOverTime);
            colorOverLifetimeModule.color = new UnityEngine.ParticleSystem.MinMaxGradient(component.CachedGradient);
        }

        private static void ApplyForceOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var forceOverLifetimeModule = particleSystem.forceOverLifetime;

            if (particleSystemData.AdditionalForce == null)
            {
                forceOverLifetimeModule.enabled = false;
                return;
            }

            forceOverLifetimeModule.enabled = true;
            forceOverLifetimeModule.space = particleSystemData.GetSimulationSpace() == PBParticleSystem.Types.SimulationSpace.PssWorld
                                            ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            forceOverLifetimeModule.x = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.AdditionalForce.X);
            forceOverLifetimeModule.y = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.AdditionalForce.Y);
            forceOverLifetimeModule.z = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.AdditionalForce.Z);
        }

        private static void ApplyLimitVelocityOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var limitVelocityModule = particleSystem.limitVelocityOverLifetime;

            if (particleSystemData.LimitVelocity == null)
            {
                limitVelocityModule.enabled = false;
                return;
            }

            limitVelocityModule.enabled = true;
            limitVelocityModule.separateAxes = false;
            limitVelocityModule.space = ParticleSystemSimulationSpace.Local;
            limitVelocityModule.limit = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.LimitVelocity.Speed);
            limitVelocityModule.dampen = particleSystemData.LimitVelocity.GetDampen();
        }

        private static void ApplySpriteSheet(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem, ref ParticleSystemComponent component)
        {
            var textureSheetAnimationModule = particleSystem.textureSheetAnimation;

            if (particleSystemData.SpriteSheet == null)
            {
                textureSheetAnimationModule.enabled = false;
                return;
            }

            textureSheetAnimationModule.enabled = true;
            textureSheetAnimationModule.mode = ParticleSystemAnimationMode.Grid;

            int tilesX = particleSystemData.SpriteSheet.TilesX > 0 ? (int)particleSystemData.SpriteSheet.TilesX : 1;
            int tilesY = particleSystemData.SpriteSheet.TilesY > 0 ? (int)particleSystemData.SpriteSheet.TilesY : 1;
            textureSheetAnimationModule.numTilesX = tilesX;
            textureSheetAnimationModule.numTilesY = tilesY;

            float framesPerSecond = particleSystemData.SpriteSheet.GetFramesPerSecond();
            textureSheetAnimationModule.timeMode = ParticleSystemAnimationTimeMode.FPS;
            textureSheetAnimationModule.fps = framesPerSecond;
        }

        private void ApplyRenderer(PBParticleSystem particleSystemData, ref ParticleSystemComponent component)
        {
            var particleRenderer = component.Renderer;

            var blendMode = particleSystemData.GetBlendMode();

            if (!component.BlendModeInitialized || component.LastAppliedBlendMode != blendMode)
            {
                EnsureMaterial(ref component, blendMode);
                component.LastAppliedBlendMode = blendMode;
                component.BlendModeInitialized = true;
            }

            // When billboard is OFF, Mesh render mode uses the prefab's default Quad mesh
            bool billboard = particleSystemData.GetBillboard();
            if (billboard)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.alignment = ParticleSystemRenderSpace.View;
            }
            else
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                particleRenderer.alignment = ParticleSystemRenderSpace.Local;
            }

            particleRenderer.material = component.ParticleMaterial;

            if (particleSystemData.Texture != null && !string.IsNullOrEmpty(particleSystemData.Texture.Src))
                PrepareTexture(particleSystemData.Texture, ref component);
            else
                component.CleanUpTexture(World);
        }

        private void EnsureMaterial(ref ParticleSystemComponent component, PBParticleSystem.Types.BlendMode blendMode)
        {
            if (component.ParticleMaterial == null)
                component.ParticleMaterial = materialPool.Get();

            ApplyBlendMode(component.ParticleMaterial, blendMode);
        }

        private static void ApplyBlendMode(Material material, PBParticleSystem.Types.BlendMode blendMode)
        {
            // All particle blend modes require a transparent surface in URP
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt(SURFACE_ID, 1);
            material.SetInt(ZWRITE_ID, 0);
            material.renderQueue = (int)RenderQueue.Transparent;

            switch (blendMode)
            {
                case PBParticleSystem.Types.BlendMode.PsbAdd:
                    material.SetInt(BLEND_MODE_ID, 2); // Additive
                    material.SetInt(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                    material.SetInt(DST_BLEND_ID, (int)BlendMode.One);
                    break;

                case PBParticleSystem.Types.BlendMode.PsbMultiply:
                    material.SetInt(BLEND_MODE_ID, 3); // Multiply
                    material.SetInt(SRC_BLEND_ID, (int)BlendMode.DstColor);
                    material.SetInt(DST_BLEND_ID, (int)BlendMode.Zero);
                    break;

                default: // PSB_ALPHA
                    material.SetInt(BLEND_MODE_ID, 0); // Alpha
                    material.SetInt(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                    material.SetInt(DST_BLEND_ID, (int)BlendMode.OneMinusSrcAlpha);
                    break;
            }
        }

        private void PrepareTexture(global::Decentraland.Common.Texture protoTexture, ref ParticleSystemComponent component)
        {
            if (!protoTexture.TryGetTextureUrl(sceneData, out URLAddress url)) return;
            protoTexture.TryGetTextureFileHash(sceneData, out string fileHash);

            TextureComponent textureComponent = new TextureComponent(
                url, fileHash,
                protoTexture.GetWrapMode(),
                protoTexture.GetFilterMode());

            if (TextureComponentUtils.Equals(textureComponent, component.LoadingTextureIntention))
                return;

            component.CleanUpTexture(World);

            var intention = new GetTextureIntention(
                textureComponent.Src,
                textureComponent.FileHash,
                textureComponent.WrapMode,
                textureComponent.FilterMode,
                textureComponent.TextureType,
                nameof(ParticleSystemApplyPropertiesSystem),
                attemptsCount: GET_TEXTURE_MAX_ATTEMPT_COUNT);

            component.LoadingTextureIntention = intention;
            component.TexturePromise = TexturePromise.Create(World, intention, partitionComponent);
        }

        [Query]
        private void ResolveTexturePromise(ref ParticleSystemComponent component)
        {
            var promise = component.TexturePromise;
            if (promise == null || promise.Value.IsConsumed) return;
            if (!promise.Value.TryConsume(World, out StreamableLoadingResult<TextureData> result)) return;

            component.TexturePromise = null;

            if (result.Asset == null) return;

            component.SourceTextureData = result.Asset;

            if (component.ParticleMaterial != null)
                component.ParticleMaterial.mainTexture = result.Asset;
        }

        private static UnityEngine.ParticleSystem.MinMaxCurve BuildLinearCurve(float start, float end, ref AnimationCurve cachedCurve)
        {
            if (cachedCurve == null)
                cachedCurve = new AnimationCurve(new Keyframe(0f, start), new Keyframe(1f, end));
            else
            {
                cachedCurve.MoveKey(0, new Keyframe(0f, start));
                cachedCurve.MoveKey(1, new Keyframe(1f, end));
            }

            return new UnityEngine.ParticleSystem.MinMaxCurve(1f, cachedCurve);
        }
    }
}
