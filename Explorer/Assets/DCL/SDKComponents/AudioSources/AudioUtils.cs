using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public static class AudioUtils
    {
        public static void CleanUp(this ref AudioSourceComponent component, World world, IDereferencableCache<AudioClip, GetAudioClipIntention> cache, IComponentPool componentPool)
        {
            component.ClipPromise.ForgetLoading(world);

            if (component.AudioSource == null) return; // loading in progress

            if (component.AudioSource.isPlaying)
                component.AudioSource.Stop();

            cache.Dereference(component.ClipPromise.LoadingIntention, component.AudioSource.clip);
        }

        public static void AddReferenceToAudioClip(this ref AudioSourceComponent component,IDereferencableCache<AudioClip, GetAudioClipIntention> cache)
        {
            cache.Add(component.ClipPromise.LoadingIntention, component.AudioSource.clip);
        }

        public static bool TryCreateAudioClipPromise(World world, ISceneData sceneData, string pbAudioClipUrl, PartitionComponent partitionComponent, out Promise? assetPromise)
        {
            if (!sceneData.TryGetContentUrl(pbAudioClipUrl, out URLAddress audioClipUrl))
            {
                assetPromise = null;
                return false;
            }

            assetPromise = Promise.Create(world, new GetAudioClipIntention
            {
                CommonArguments = new CommonLoadingArguments(audioClipUrl),
                AudioType = pbAudioClipUrl.ToAudioType(),
            }, partitionComponent);

            return true;
        }

        public static Promise CreateAudioClipPromise(World world, string url, AudioType audioType, IPartitionComponent partitionComponent) =>
            Promise.Create(world, new GetAudioClipIntention
            {
                CommonArguments = new CommonLoadingArguments(url),
                AudioType = audioType,
            }, partitionComponent);

        public static AudioType ToAudioType(this string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                ReportHub.LogError(ReportCategory.SDK_AUDIO_SOURCES, $"Cannot detect AudioType. UrlName doesn't contain file extension!. Setting to {AudioType.UNKNOWN.ToString()}");
                return AudioType.UNKNOWN;
            }

            ReadOnlySpan<char> ext = url.AsSpan()[^3..];

            if (ext.Equals("mp3", StringComparison.OrdinalIgnoreCase))
                return AudioType.MPEG;

            if (ext.Equals("wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;

            if (ext.Equals("ogg", StringComparison.OrdinalIgnoreCase))
                return AudioType.OGGVORBIS;

            return AudioType.UNKNOWN;
        }
    }
}
