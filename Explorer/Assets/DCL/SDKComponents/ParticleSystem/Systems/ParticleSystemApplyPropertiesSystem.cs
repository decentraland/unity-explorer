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
        private static readonly int BLEND_OP_ID = Shader.PropertyToID("_BlendOp");
        private static readonly int COLOR_MASK_ID = Shader.PropertyToID("_ColorMask");

        private readonly ISceneData sceneData;
        private readonly IPartitionComponent partitionComponent;

        internal ParticleSystemApplyPropertiesSystem(World world, ISceneData sceneData, IPartitionComponent partitionComponent) : base(world)
        {
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
        }

        protected override void Update(float t)
        {
            ApplyParticleSystemPropertiesQuery(World);
            ResolveTexturePromiseQuery(World);
        }

        [Query]
        private void ApplyParticleSystemProperties(ref PBParticleSystem pb, ref ParticleSystemComponent component)
        {
            if (!pb.IsDirty) return;

            var ps = component.ParticleSystemInstance;

            ApplyMain(pb, ps);
            ApplyEmission(pb, ps);
            ApplyShape(pb, ps);
            ApplySizeOverLifetime(pb, ps);
            ApplyRotationOverLifetime(pb, ps);
            ApplyColorOverLifetime(pb, ps);
            ApplyForceOverLifetime(pb, ps);
            ApplySpriteSheet(pb, ps);
            ApplyRenderer(pb, ref component);
        }

        private static void ApplyMain(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var main = ps.main;

            main.loop = true;

            if (pb.HasLifetime)
                main.startLifetime = pb.Lifetime;

            if (pb.HasMaxParticles)
                main.maxParticles = (int)pb.MaxParticles;

            if (pb.HasGravity)
                main.gravityModifier = pb.Gravity;

            // Initial size: random between start and end
            if (pb.InitialSize != null)
                main.startSize = new UnityEngine.ParticleSystem.MinMaxCurve(pb.InitialSize.Start, pb.InitialSize.End);

            // Initial rotation: random between start and end (convert degrees to radians for Unity)
            if (pb.InitialRotation != null)
                main.startRotation = new UnityEngine.ParticleSystem.MinMaxCurve(
                    pb.InitialRotation.Start * Mathf.Deg2Rad,
                    pb.InitialRotation.End * Mathf.Deg2Rad);

            // Initial color: random between start and end
            if (pb.InitialColor != null)
            {
                var startColor = ToUnityColor(pb.InitialColor.Start);
                var endColor = ToUnityColor(pb.InitialColor.End);
                main.startColor = new UnityEngine.ParticleSystem.MinMaxGradient(startColor, endColor);
            }

            // Initial speed: random between start and end
            if (pb.InitialVelocitySpeed != null)
                main.startSpeed = new UnityEngine.ParticleSystem.MinMaxCurve(pb.InitialVelocitySpeed.Start, pb.InitialVelocitySpeed.End);
        }

        private static void ApplyEmission(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var emission = ps.emission;
            bool active = !pb.HasActive || pb.Active;
            emission.enabled = active;

            if (pb.HasRate)
                emission.rateOverTime = pb.Rate;
        }

        private static void ApplyShape(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = true;

            if (pb.ShapeCase == PBParticleSystem.ShapeOneofCase.None)
            {
                // Default: point emitter
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0f;
                return;
            }

            switch (pb.ShapeCase)
            {
                case PBParticleSystem.ShapeOneofCase.Point:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Sphere:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = pb.Sphere.HasRadius ? pb.Sphere.Radius : 1f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Cone:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = pb.Cone.HasAngle ? pb.Cone.Angle : 25f;
                    shape.radius = pb.Cone.HasRadius ? pb.Cone.Radius : 1f;
                    break;

                case PBParticleSystem.ShapeOneofCase.Box:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    if (pb.Box.Size != null)
                        shape.scale = new Vector3(pb.Box.Size.X, pb.Box.Size.Y, pb.Box.Size.Z);
                    break;
            }
        }

        private static void ApplySizeOverLifetime(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var sol = ps.sizeOverLifetime;

            if (pb.SizeOverTime == null)
            {
                sol.enabled = false;
                return;
            }

            sol.enabled = true;
            sol.separateAxes = false;

            // Linear lerp from start to end over particle lifetime using a two-key curve
            sol.size = BuildLinearCurve(pb.SizeOverTime.Start, pb.SizeOverTime.End);
        }

        private static void ApplyRotationOverLifetime(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var rol = ps.rotationOverLifetime;

            if (pb.RotationOverTime == null)
            {
                rol.enabled = false;
                return;
            }

            rol.enabled = true;
            rol.separateAxes = false;
            // Degrees/sec → radians/sec
            rol.z = new UnityEngine.ParticleSystem.MinMaxCurve(
                pb.RotationOverTime.Start * Mathf.Deg2Rad,
                pb.RotationOverTime.End * Mathf.Deg2Rad);
        }

        private static void ApplyColorOverLifetime(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;

            if (pb.ColorOverTime == null)
            {
                col.enabled = false;
                return;
            }

            col.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(ToUnityColor(pb.ColorOverTime.Start), 0f), new GradientColorKey(ToUnityColor(pb.ColorOverTime.End), 1f) },
                new[] { new GradientAlphaKey(pb.ColorOverTime.Start.A, 0f), new GradientAlphaKey(pb.ColorOverTime.End.A, 1f) }
            );

            col.color = new UnityEngine.ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ApplyForceOverLifetime(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var fol = ps.forceOverLifetime;

            if (pb.AdditionalForce == null)
            {
                fol.enabled = false;
                return;
            }

            fol.enabled = true;
            fol.space = ParticleSystemSimulationSpace.World;
            fol.x = new UnityEngine.ParticleSystem.MinMaxCurve(pb.AdditionalForce.X);
            fol.y = new UnityEngine.ParticleSystem.MinMaxCurve(pb.AdditionalForce.Y);
            fol.z = new UnityEngine.ParticleSystem.MinMaxCurve(pb.AdditionalForce.Z);
        }

        private static void ApplySpriteSheet(PBParticleSystem pb, UnityEngine.ParticleSystem ps)
        {
            var tsa = ps.textureSheetAnimation;

            if (pb.SpriteSheet == null)
            {
                tsa.enabled = false;
                return;
            }

            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;

            int tilesX = pb.SpriteSheet.TilesX > 0 ? (int)pb.SpriteSheet.TilesX : 1;
            int tilesY = pb.SpriteSheet.TilesY > 0 ? (int)pb.SpriteSheet.TilesY : 1;
            tsa.numTilesX = tilesX;
            tsa.numTilesY = tilesY;

            int totalFrames = tilesX * tilesY;
            int startFrame = (int)pb.SpriteSheet.StartFrame;
            int endFrame = pb.SpriteSheet.EndFrame > 0 ? (int)pb.SpriteSheet.EndFrame : totalFrames - 1;

            tsa.frameOverTime = BuildLinearCurve(startFrame, endFrame);

            float cycles = pb.SpriteSheet.HasCyclesPerLifetime ? pb.SpriteSheet.CyclesPerLifetime : 1f;
            tsa.cycleCount = Mathf.RoundToInt(cycles);
        }

        private void ApplyRenderer(PBParticleSystem pb, ref ParticleSystemComponent component)
        {
            var renderer = component.ParticleSystemInstance.GetComponent<ParticleSystemRenderer>();

            bool billboard = !pb.HasBillboard || pb.Billboard;
            renderer.renderMode = billboard ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;

            var blendMode = pb.HasBlendMode ? pb.BlendMode : PBParticleSystem.Types.BlendMode.PsbAlpha;
            EnsureMaterial(ref component, blendMode);
            renderer.material = component.ParticleMaterial;

            if (pb.Texture != null)
                PrepareTexture(pb.Texture, ref component);
            else
                component.CleanUpTexture(World);
        }

        private static void EnsureMaterial(ref ParticleSystemComponent component, PBParticleSystem.Types.BlendMode blendMode)
        {
            if (component.ParticleMaterial == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
                component.ParticleMaterial = new Material(shader);
            }

            switch (blendMode)
            {
                case PBParticleSystem.Types.BlendMode.PsbAdd:
                    component.ParticleMaterial.SetInt(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                    component.ParticleMaterial.SetInt(DST_BLEND_ID, (int)BlendMode.One);
                    component.ParticleMaterial.SetInt(BLEND_OP_ID, (int)BlendOp.Add);
                    component.ParticleMaterial.SetInt(COLOR_MASK_ID, (int)ColorWriteMask.All);
                    break;

                case PBParticleSystem.Types.BlendMode.PsbMultiply:
                    component.ParticleMaterial.SetInt(SRC_BLEND_ID, (int)BlendMode.DstColor);
                    component.ParticleMaterial.SetInt(DST_BLEND_ID, (int)BlendMode.Zero);
                    component.ParticleMaterial.SetInt(BLEND_OP_ID, (int)BlendOp.Add);
                    component.ParticleMaterial.SetInt(COLOR_MASK_ID, (int)ColorWriteMask.All);
                    break;

                default: // PSB_ALPHA
                    component.ParticleMaterial.SetInt(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                    component.ParticleMaterial.SetInt(DST_BLEND_ID, (int)BlendMode.OneMinusSrcAlpha);
                    component.ParticleMaterial.SetInt(BLEND_OP_ID, (int)BlendOp.Add);
                    component.ParticleMaterial.SetInt(COLOR_MASK_ID, (int)ColorWriteMask.All);
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

        private static Color ToUnityColor(global::Decentraland.Common.Color4 c) =>
            new Color(c.R, c.G, c.B, c.A);
    }
}
