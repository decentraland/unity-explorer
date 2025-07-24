using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.NFTShape.Component;
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
            InitializeNftMaterialQuery(World);
        }

        [Query]
        public void InitializeMaterial(Entity entity, in InitializeVideoPlayerMaterialRequest request)
        {
            if (!TryHandleRequest<InitializeVideoPlayerMaterialRequest>(entity, request.MediaPlayerComponentEntity, out Vector2 texScale)) return;

            var material = request.Renderer.sharedMaterial;
            material.SetTextureScale(ShaderUtils.BaseMap, texScale);

            if (material.shader.name != "DCL/Scene") // Scene shader doesn't have _AlphaTexture
                material.SetTextureScale(ShaderUtils.AlphaTexture, texScale);
        }

        [Query]
        public void InitializeNftMaterial(Entity entity, in InitializeNftVideoMaterialRequest request)
        {
            if (!TryHandleRequest<InitializeNftVideoMaterialRequest>(entity, request.MediaPlayerComponentEntity, out Vector2 texScale)) return;

            request.Renderer.SetTextureScale(texScale);
        }

        private bool TryHandleRequest<T>(Entity entity, in Entity mediaPlayerComponentEntity, out Vector2 texScale)
        {
            texScale = Vector2.zero;

            //Media Player Component not yet initialized
            if (!World.TryGet(mediaPlayerComponentEntity, out MediaPlayerComponent mediaPlayerComponent))
                return false;

            if (!mediaPlayerComponent.MediaPlayer.IsReady)
            {
                if (mediaPlayerComponent.MediaPlayer.IsAvProPlayer(out AvProPlayer? _))
                {
                    // AV Pro player not initialized yet (should not happen, but we can just wait)
                    ReportHub.LogWarning(GetReportCategory(), $"Handling {nameof(InitializeVideoPlayerMaterialRequest)} before the AV Pro player was initialized");
                }
                return false;
            }

            texScale = mediaPlayerComponent.MediaPlayer.GetTexureScale;

            World.Destroy(entity);

            return true;
        }
    }
}
