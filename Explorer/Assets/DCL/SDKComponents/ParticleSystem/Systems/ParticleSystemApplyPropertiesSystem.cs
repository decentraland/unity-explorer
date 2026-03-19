using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.Groups;
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
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemLifecycleSystem))]
    [ThrottlingEnabled]
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
            ApplyEmission(particleSystemData, particleSystem);
            ApplyShape(particleSystemData, particleSystem);
            ApplySizeOverLifetime(particleSystemData, particleSystem);
            ApplyRotationOverLifetime(particleSystemData, particleSystem);
            ApplyColorOverLifetime(particleSystemData, particleSystem);
            ApplyForceOverLifetime(particleSystemData, particleSystem);
            ApplyLimitVelocityOverLifetime(particleSystemData, particleSystem);
            ApplySpriteSheet(particleSystemData, particleSystem);
            ApplyRenderer(particleSystemData, ref component);
        }

        private static void ApplyMain(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var mainModule = particleSystem.main;

            mainModule.loop = !particleSystemData.HasLoop || particleSystemData.Loop;
            mainModule.prewarm = mainModule.loop && particleSystemData.HasPrewarm && particleSystemData.Prewarm;

            mainModule.simulationSpace = particleSystemData is { HasSimulationSpace: true, SimulationSpace: PBParticleSystem.Types.SimulationSpace.PssWorld }
                ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;

            if (particleSystemData.HasLifetime)
                mainModule.startLifetime = particleSystemData.Lifetime;

            if (particleSystemData.HasMaxParticles)
                mainModule.maxParticles = (int)particleSystemData.MaxParticles;

            if (particleSystemData.HasGravity)
                mainModule.gravityModifier = particleSystemData.Gravity;

            // Initial size: random between start and end
            if (particleSystemData.InitialSize != null)
                mainModule.startSize = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.InitialSize.Start, particleSystemData.InitialSize.End);

            // Initial rotation: random between start and end (convert degrees to radians for Unity)
            if (particleSystemData.InitialRotation != null)
                mainModule.startRotation = new UnityEngine.ParticleSystem.MinMaxCurve(
                    particleSystemData.InitialRotation.Start * Mathf.Deg2Rad,
                    particleSystemData.InitialRotation.End * Mathf.Deg2Rad);

            // Initial color: random between start and end
            if (particleSystemData.InitialColor != null)
            {
                var startColor = ToUnityColor(particleSystemData.InitialColor.Start);
                var endColor = ToUnityColor(particleSystemData.InitialColor.End);
                mainModule.startColor = new UnityEngine.ParticleSystem.MinMaxGradient(startColor, endColor);
            }

            // Initial speed: random between start and end
            if (particleSystemData.InitialVelocitySpeed != null)
                mainModule.startSpeed = new UnityEngine.ParticleSystem.MinMaxCurve(particleSystemData.InitialVelocitySpeed.Start, particleSystemData.InitialVelocitySpeed.End);
        }

        private static void ApplyEmission(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var emissionModule = particleSystem.emission;
            bool active = !particleSystemData.HasActive || particleSystemData.Active;
            emissionModule.enabled = active;

            if (particleSystemData.HasRate)
                emissionModule.rateOverTime = particleSystemData.Rate;
        }

        private static void ApplyShape(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var shapeModule = particleSystem.shape;
            shapeModule.enabled = true;

            if (particleSystemData.ShapeCase == PBParticleSystem.ShapeOneofCase.None)
            {
                // Default: point emitter
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                shapeModule.radius = 0f;
                return;
            }

            shapeModule.scale = Vector3.one;
            switch (particleSystemData.ShapeCase)
            {
                case PBParticleSystem.ShapeOneofCase.Point:
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radius = 0f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Sphere:
                    shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                    shapeModule.radius = particleSystemData.Sphere.HasRadius ? particleSystemData.Sphere.Radius : 1f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Cone:
                    shapeModule.shapeType = ParticleSystemShapeType.Cone;
                    shapeModule.angle = particleSystemData.Cone.HasAngle ? particleSystemData.Cone.Angle : 25f;
                    shapeModule.radius = particleSystemData.Cone.HasRadius ? particleSystemData.Cone.Radius : 1f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Box:
                    shapeModule.shapeType = ParticleSystemShapeType.Box;
                    if (particleSystemData.Box.Size != null)
                        shapeModule.scale = new Vector3(particleSystemData.Box.Size.X, particleSystemData.Box.Size.Y, particleSystemData.Box.Size.Z);
                    break;
            }
        }

        private static void ApplySizeOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
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
            sizeOverLifetimeModule.size = BuildLinearCurve(particleSystemData.SizeOverTime.Start, particleSystemData.SizeOverTime.End);
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
            rotationOverLifetimeModule.separateAxes = false;
            // Degrees/sec → radians/sec
            rotationOverLifetimeModule.z = new UnityEngine.ParticleSystem.MinMaxCurve(
                particleSystemData.RotationOverTime.Start * Mathf.Deg2Rad,
                particleSystemData.RotationOverTime.End * Mathf.Deg2Rad);
        }

        private static void ApplyColorOverLifetime(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
        {
            var colorOverLifetimeModule = particleSystem.colorOverLifetime;

            if (particleSystemData.ColorOverTime == null)
            {
                colorOverLifetimeModule.enabled = false;
                return;
            }

            colorOverLifetimeModule.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(ToUnityColor(particleSystemData.ColorOverTime.Start), 0f), new GradientColorKey(ToUnityColor(particleSystemData.ColorOverTime.End), 1f) },
                new[] { new GradientAlphaKey(particleSystemData.ColorOverTime.Start.A, 0f), new GradientAlphaKey(particleSystemData.ColorOverTime.End.A, 1f) }
            );

            colorOverLifetimeModule.color = new UnityEngine.ParticleSystem.MinMaxGradient(gradient);
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
            forceOverLifetimeModule.space = particleSystemData is { HasSimulationSpace: true, SimulationSpace: PBParticleSystem.Types.SimulationSpace.PssWorld }
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
            limitVelocityModule.dampen = particleSystemData.LimitVelocity.HasDampen ? particleSystemData.LimitVelocity.Dampen : 1f;
        }

        private static void ApplySpriteSheet(PBParticleSystem particleSystemData, UnityEngine.ParticleSystem particleSystem)
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

            int totalFrames = tilesX * tilesY;
            int startFrame = (int)particleSystemData.SpriteSheet.StartFrame;
            int endFrame = particleSystemData.SpriteSheet.EndFrame > 0 ? (int)particleSystemData.SpriteSheet.EndFrame : totalFrames - 1;

            textureSheetAnimationModule.frameOverTime = BuildLinearCurve(startFrame, endFrame);

            float framesPerSecond = particleSystemData.SpriteSheet.HasFramesPerSecond ? particleSystemData.SpriteSheet.FramesPerSecond : 30f;
            textureSheetAnimationModule.timeMode = ParticleSystemAnimationTimeMode.FPS;
            textureSheetAnimationModule.fps = framesPerSecond;
        }

        private void ApplyRenderer(PBParticleSystem particleSystemData, ref ParticleSystemComponent component)
        {
            var particleRenderer = component.ParticleSystemInstance.GetComponent<ParticleSystemRenderer>();

            bool billboard = !particleSystemData.HasBillboard || particleSystemData.Billboard;
            particleRenderer.renderMode = billboard ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;

            var blendMode = particleSystemData.HasBlendMode ? particleSystemData.BlendMode : PBParticleSystem.Types.BlendMode.PsbAlpha;
            EnsureMaterial(ref component, blendMode);
            particleRenderer.material = component.ParticleMaterial;

            if (particleSystemData.Texture != null)
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
            if (string.IsNullOrEmpty(protoTexture.Src)) return;

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

            if (component.ParticleMaterial != null)
                component.ParticleMaterial.mainTexture = result.Asset;
        }

        private static UnityEngine.ParticleSystem.MinMaxCurve BuildLinearCurve(float start, float end)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(1f, end));
            return new UnityEngine.ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static Color ToUnityColor(global::Decentraland.Common.Color4 protoColor) =>
            new Color(protoColor.R, protoColor.G, protoColor.B, protoColor.A);
    }
}
