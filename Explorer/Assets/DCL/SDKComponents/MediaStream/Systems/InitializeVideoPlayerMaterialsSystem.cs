using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Shaders;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class InitializeVideoPlayerMaterialsSystem : BaseUnityLoopSystem
    {
        public InitializeVideoPlayerMaterialsSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            InitializeMaterialQuery(World);
        }

        [Query]
        public void InitializeMaterial(Entity entity, in MediaPlayerComponent mediaPlayerComponent, in InitializeVideoPlayerMaterialRequest request)
        {
            if (!mediaPlayerComponent.MediaPlayer.IsAvProPlayer(out var avPro))
            {
                // We only need to handle AV Pro players
                World.Remove<InitializeVideoPlayerMaterialRequest>(entity);
                return;
            }

            var textureProducer = avPro!.Value.AvProMediaPlayer.TextureProducer;
            if (textureProducer == null)
            {
                // AV Pro player not initialized yet (should not happen, but we can just wait)
                ReportHub.LogWarning(GetReportCategory(), $"Handling {nameof(InitializeVideoPlayerMaterialRequest)} before the AV Pro player was initialized");
                return;
            }

            float vScale = textureProducer.RequiresVerticalFlip() ? -1 : 1;
            var texScale = new Vector2(1, vScale);

            var material = request.Renderer.sharedMaterial;
            material.SetTextureScale(ShaderUtils.BaseMap, texScale);
            material.SetTextureScale(ShaderUtils.AlphaTexture, texScale);

            World.Remove<InitializeVideoPlayerMaterialRequest>(entity);
        }
    }
}
