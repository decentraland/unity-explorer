using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CleanUpMediaPlayerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        internal CleanUpMediaPlayerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            HandleOriginalComponentRemovalQuery(World);
            RemoveVideoPriorityQuery(World);

            TryReleaseConsumerQuery(World);
            HandleTextureWithoutConsumersQuery(World);

            HandleMediaPlayerDestructionQuery(World);
        }

        /// <summary>
        ///     Removes Media Player when the component that originated it is removed.
        /// </summary>
        [Query]
        [None(typeof(PBAudioStream), typeof(CustomMediaStream), typeof(PBVideoPlayer))]
        private void HandleOriginalComponentRemoval(Entity e, ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
            World.Remove<MediaPlayerComponent>(e);
        }

        /// <summary>
        ///     Removes <see cref="VideoStateByPriorityComponent" /> component when the attached Media Player is removed
        /// </summary>
        [Query]
        [All(typeof(VideoStateByPriorityComponent))]
        [None(typeof(MediaPlayerComponent))]
        private void RemoveVideoPriority(Entity e) =>
            World.Remove<VideoStateByPriorityComponent>(e);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleMediaPlayerDestruction(ref MediaPlayerComponent mediaPlayer)
        {
            CleanUpMediaPlayer(ref mediaPlayer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryReleaseConsumer(Entity entity, ref VideoTextureConsumer textureConsumer)
        {
            CleanUpVideoTexture(ref textureConsumer);
            World.Remove<VideoTextureConsumer>(entity);
        }

        /// <summary>
        ///     Prevents CPU and memory leaks by cleaning up video textures and media players that are not being used anymore.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleTextureWithoutConsumers(Entity entity, ref VideoTextureConsumer textureConsumer, TextureData textureData)
        {
            if (textureData.referenceCount == 0)
            {
                CleanUpVideoTexture(ref textureConsumer);
                World.Remove<VideoTextureConsumer>(entity);
            }
        }

        private void CleanUpMediaPlayer(ref MediaPlayerComponent mediaPlayerComponent) =>
            mediaPlayerComponent.Dispose();

        public void FinalizeComponents(in Query query)
        {
            FinalizeMediaPlayerComponentQuery(World);
            FinalizeVideoTextureConsumerComponentQuery(World);
        }

        private void CleanUpVideoTexture(ref VideoTextureConsumer videoTextureConsumer) =>
            videoTextureConsumer.Dispose();

        [Query]
        private void FinalizeMediaPlayerComponent(ref MediaPlayerComponent component) =>
            CleanUpMediaPlayer(ref component);

        [Query]
        private void FinalizeVideoTextureConsumerComponent(ref VideoTextureConsumer component) =>
            CleanUpVideoTexture(ref component);
    }
}
