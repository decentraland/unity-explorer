using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.NFTShape.Component;
using DCL.Shaders;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Textures.Components;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class UpdateVideoMaterialTextureScaleSystem : BaseUnityLoopSystem
    {
        public UpdateVideoMaterialTextureScaleSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateMaterialQuery(World);
        }

        [Query]
        public void UpdateMaterial(in MediaPlayerComponent mediaPlayerComponent, ref VideoTextureConsumer videoTextureConsumer)
        {
            if (!videoTextureConsumer.IsDirty)
                return;

            if (!TryHandleRequest(mediaPlayerComponent, out var texScale)) return;
            videoTextureConsumer.SetTextureScale(texScale);
            videoTextureConsumer.IsDirty = false;
        }

        private bool TryHandleRequest(in MediaPlayerComponent mediaPlayerComponent, out Vector2 texScale)
        {
            texScale = Vector2.zero;

            if (!mediaPlayerComponent.MediaPlayer.IsReady)
            {
                if (mediaPlayerComponent.MediaPlayer.IsAvProPlayer(out AvProPlayer? _))
                {
                    // AV Pro player not initialized yet (should not happen, but we can just wait)
                    ReportHub.LogWarning(GetReportCategory(), $"Handling {nameof(UpdateVideoMaterialTextureScaleSystem)} before the AV Pro player was initialized");
                }
                return false;
            }
            texScale = mediaPlayerComponent.MediaPlayer.GetTexureScale;
            return true;
        }
    }
}
