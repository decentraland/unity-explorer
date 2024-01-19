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
            videoMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            videoTexture = new Texture2D(426, 240, TextureFormat.BGRA32, false, false);
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
            // meshRenderer.MeshRenderer.sharedMaterial = videoMat;
            MediaPlayer? mediaPlayer = mediaPlayerPool.Get();

            if (videoMat == null)
                videoMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            // var videoMaterialComponent = new VideoMaterialComponent();
            var component = new VideoPlayerComponent(sdkVideo, mediaPlayer);
            World.Add(entity, component);
        }

        [Query]
        private void UpdateVideoStreamTexture(ref VideoPlayerComponent mediaPlayer)
        {
            if (mediaPlayer.mediaPlayer.TextureProducer != null)
            {
                Texture avText = mediaPlayer.mediaPlayer.TextureProducer.GetTexture();
                UpdateVideoTexture(avText, mediaPlayer.mediaPlayer);
            }
        }

        public void UpdateVideoTexture(Texture avProTexture, MediaPlayer avProMediaPlayer)
        {
            if (avProTexture && HasEqualResolution(avProTexture))
            {
                avProTexture = avProMediaPlayer.TextureProducer.GetTexture(0);
                Graphics.CopyTexture(avProTexture, videoTexture);

                videoMat.mainTexture = videoTexture;
            }
            else if (avProTexture && !HasEqualResolution(avProTexture))
            {
                ResizeVideoTexture(avProMediaPlayer, avProTexture);
            }
        }

        private bool HasEqualResolution(Texture avProTexture)
        {
            return avProTexture.width == videoTexture.width && avProTexture.height == videoTexture.height;
        }

        private bool isResizing;
        public async UniTask ResizeVideoTexture(MediaPlayer avProMediaPlayer, Texture avText)
        {
            if (isResizing) return;
            avText = null;
            isResizing = true;
            while (videoTexture == null || avText == null)
            {
                avText = avProMediaPlayer.TextureProducer.GetTexture(0);
                await UniTask.Yield();
            }
            videoTexture.Reinitialize(avText.width, avText.height);
            videoTexture.Apply();

            isResizing = false;
        }
    }
}
