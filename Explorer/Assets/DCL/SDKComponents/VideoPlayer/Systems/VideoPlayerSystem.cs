using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using RenderHeads.Media.AVProVideo;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoPlayerSystem : BaseUnityLoopSystem
    {
        // Refs VideoPlayerHandler, AvProVideoPlayer : IVideoPlayer, VideoPluginWrapper_AVPro, DCLVideoTexture
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;
        private readonly Texture2D videoTexture;

        private readonly string matPath = "Assets/Scripts/ECS/Unity/Materials/MaterialReference/VideoMaterial.mat";
        private Material videoMat;

        private VideoPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool) : base(world)
        {
            this.mediaPlayerPool = mediaPlayerPool;

            videoTexture = new Texture2D(1, 1, TextureFormat.BGRA32, false, false);

            videoMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            videoMat.mainTexture = videoTexture;
        }

        protected override void Update(float t)
        {
            InstantiateVideoStreamQuery(World);
            UpdateVideoStreamTextureQuery(World);
        }

        [Query]
        [None(typeof(VideoPlayerComponent))]
        private void InstantiateVideoStream(in Entity entity, ref PBVideoPlayer sdkVideo, ref PBMaterial sdkMaterial, ref PrimitiveMeshRendererComponent meshRenderer)
        {
            MediaPlayer? mediaPlayer = mediaPlayerPool.Get();

            if (videoMat == null)
            {
                videoMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                videoMat.mainTexture = videoTexture;
            }

            // var videoMaterialComponent = new VideoMaterialComponent();
            var component = new VideoPlayerComponent(sdkVideo, mediaPlayer);
            World.Add(entity, component);
        }

        [Query]
        private void UpdateVideoStreamTexture(ref VideoPlayerComponent mediaPlayer, ref PrimitiveMeshRendererComponent meshRenderer)
        {
            Texture avText = mediaPlayer.mediaPlayer.TextureProducer.GetTexture();
            if (avText == null) return;

            meshRenderer.MeshRenderer.sharedMaterial = videoMat;

            if (videoTexture.HasEqualResolution(to: avText))
                UpdateVideoTexture(avText);
            else
                ResizeVideoTexture(avText); // will be updated on the next frame/update-loop
        }

        private void UpdateVideoTexture(Texture avText)
        {
            Graphics.CopyTexture(avText, videoTexture);

            if (videoMat.mainTexture == null)
                videoMat.mainTexture = videoTexture;
        }

        private void ResizeVideoTexture(Texture avTexture)
        {
            videoTexture.Reinitialize(avTexture.width, avTexture.height);
            videoTexture.Apply();
        }
    }
}
