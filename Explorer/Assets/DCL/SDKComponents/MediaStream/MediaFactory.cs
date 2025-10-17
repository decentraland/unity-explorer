using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Textures.Components;
using LiveKit.Rooms;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     A unique instance per scene
    /// </summary>
    public class MediaFactory : IMediaFactory
    {
        private const string CONTENT_SERVER_PREFIX = "/content/contents";

        private readonly ISceneData sceneData;
        private readonly IRoom streamingRoom;
        private readonly MediaPlayerCustomPool mediaPlayerPool;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly MediaVolume mediaVolume;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly IWebRequestController webRequestController;
        private readonly IPerformanceBudget frameBudget;
        private readonly World world;

        private readonly IObjectPool<RenderTexture> videoTexturesPool;

        public MediaFactory(ISceneData sceneData, IRoom streamingRoom, MediaPlayerCustomPool mediaPlayerPool, ISceneStateProvider sceneStateProvider, MediaVolume mediaVolume,
            IObjectPool<RenderTexture> videoTexturesPool, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, World world, IWebRequestController webRequestController, IPerformanceBudget frameBudget)
        {
            this.sceneData = sceneData;
            this.streamingRoom = streamingRoom;
            this.mediaPlayerPool = mediaPlayerPool;
            this.videoTexturesPool = videoTexturesPool;
            this.entitiesMap = entitiesMap;
            this.world = world;
            this.webRequestController = webRequestController;
            this.frameBudget = frameBudget;
            this.sceneStateProvider = sceneStateProvider;
            this.mediaVolume = mediaVolume;
        }

        internal float worldVolumePercentage => mediaVolume.WorldVolumePercentage;

        internal float masterVolumePercentage => mediaVolume.MasterVolumePercentage;

        /// <summary>
        ///     Creates both <see cref="VideoTextureConsumer" /> and <see cref="VideoTextureData" />
        /// </summary>
        /// <returns></returns>
        public VideoTextureData CreateVideoPlayback(string url)
        {
            VideoTextureConsumer consumer = CreateVideoConsumer();
            MediaPlayerComponent mediaPlayer = CreateMediaPlayerComponent(url, false, MediaPlayerComponent.DEFAULT_VOLUME);

            return new VideoTextureData(consumer, mediaPlayer);
        }

        public VideoTextureConsumer CreateVideoConsumer()
        {
            // var renderTexture = new RenderTexture(1, 1, 0, RenderTextureFormat.BGRA32) { useMipMap = false, autoGenerateMips = false };
            // renderTexture.Create();

            var consumer = new VideoTextureConsumer(videoTexturesPool);
            return consumer;
        }

        public bool TryAddConsumer(Entity consumerEntity, CRDTEntity videoPlayerCrdtEntity, [NotNullWhen(true)] out TextureData? resultData)
        {
            resultData = null;

            if (!entitiesMap.TryGetValue(videoPlayerCrdtEntity, out Entity videoPlayerEntity) || !world.IsAlive(videoPlayerEntity))
                return false;

            // Wait until Player is created on the video player entity

            if (!world.TryGet(videoPlayerEntity, out VideoTextureConsumer consumer) || !world.TryGet(videoPlayerEntity, out MediaPlayerComponent mediaPlayer))
                return false;

            // Only TextureData contains referencing mechanism
            // Create or get it from the entity
            ref TextureData textureData = ref world.TryGetRef<TextureData>(videoPlayerEntity, out bool hasRefCounter);

            if (!hasRefCounter)
                world.Add(videoPlayerEntity, textureData = new TextureData(AnyTexture.FromVideoTextureData(new VideoTextureData(consumer, mediaPlayer))));

            if (world.TryGet(consumerEntity, out PrimitiveMeshRendererComponent primitiveMeshComponent))
                consumer.AddConsumer(primitiveMeshComponent.MeshRenderer);
            else if (world.TryGet(consumerEntity, out GltfNode gltfNode))
                foreach (Renderer? renderer in gltfNode.Renderers)
                    consumer.AddConsumer(renderer);

            textureData.AddReference();

            resultData = textureData;
            return true;
        }

        /// <summary>
        ///     Create media player with budgeting
        /// </summary>
        public bool TryCreateMediaPlayer(string url, bool hasVolume, float volume, out MediaPlayerComponent component)
        {
            if (!frameBudget.TrySpendBudget())
            {
                component = default(MediaPlayerComponent);
                return false;
            }

            component = CreateMediaPlayerComponent(url, hasVolume, volume);
            return true;
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")]
        public MediaPlayerComponent CreateMediaPlayerComponent(string url, bool hasVolume, float volume)
        {
            bool isValidLocalPath = false;
            bool isValidStreamUrl = false;

            if (url.IsLivekitAddress())
            {
                isValidLocalPath = true;
                isValidStreamUrl = true;
            }

            else

                // if it is not valid, we try get it as a scene local video
            {
                isValidStreamUrl = url.IsValidUrl();

                if (!isValidStreamUrl)
                {
                    isValidLocalPath = sceneData.TryGetMediaUrl(url, out URLAddress mediaUrl);

                    if (isValidLocalPath)
                        url = mediaUrl;
                }
            }

            var address = MediaAddress.New(url);

            MultiMediaPlayer player = address.Match(
                (streamingRoom, mediaPlayerPool),
                onUrlMediaAddress: static (ctx, address) => MultiMediaPlayer.FromAvProPlayer(new AvProPlayer(ctx.mediaPlayerPool.GetOrCreateReusableMediaPlayer(address.Url), ctx.mediaPlayerPool)),
                onLivekitAddress: static (ctx, _) => MultiMediaPlayer.FromLivekitPlayer(new LivekitPlayer(ctx.streamingRoom))
            );

            var component = new MediaPlayerComponent(player, url.Contains(CONTENT_SERVER_PREFIX))
            {
                MediaAddress = address,
                PreviousCurrentTimeChecked = -1,
                LastPropagatedState = VideoState.VsPaused,
                LastPropagatedVideoTime = 0,
                Cts = new CancellationTokenSource(),
                OpenMediaPromise = new OpenMediaPromise(),
            };

            component.SetState(isValidStreamUrl || isValidLocalPath || string.IsNullOrEmpty(url) ? VideoState.VsNone : VideoState.VsError);

            float targetVolume = (hasVolume ? volume : MediaPlayerComponent.DEFAULT_VOLUME) * worldVolumePercentage * masterVolumePercentage;
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent ? targetVolume : 0f);

            if (component.State != VideoState.VsError)
                component.OpenMediaPromise.UrlReachabilityResolveAsync(webRequestController, component.MediaAddress, ReportCategory.MEDIA_STREAM, component.Cts.Token).SuppressCancellationThrow().Forget();

            // There is no way to set this from the scene code, at the moment
            // If the player has no transform, it will appear at 0,0,0 and nobody will hear it if it is in 3D
            if (component.MediaPlayer.TryGetAvProPlayer(out MediaPlayer? mediaPlayer) && mediaPlayer!.TryGetComponent(out AudioSource mediaPlayerAudio))

                // At the moment we consider streams as global audio always, until there is a way to change it from the scene
                mediaPlayerAudio!.spatialBlend = 0.0f;

            return component;
        }
    }
}
