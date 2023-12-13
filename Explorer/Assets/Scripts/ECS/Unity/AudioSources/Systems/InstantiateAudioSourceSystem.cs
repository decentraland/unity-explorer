using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using DCL.WebRequests.AudioClips;
using ECS.Abstract;
using ECS.Unity.AudioSources.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.Unity.AudioSources.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioSourceSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IWebRequestController webRequestController;
        private readonly IComponentPool<AudioSource> audioSourcesPool;

        private InstantiateAudioSourceSystem(World world, ISceneData sceneData, IComponentPoolsRegistry poolsRegistry, IWebRequestController webRequestController) : base(world)
        {
            this.sceneData = sceneData;
            this.webRequestController = webRequestController;
            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>();
        }

        protected override void Update(float t)
        {
            InstantiateAudioSourceQuery(World);
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(TransformComponent))]
        [None(typeof(AudioSourceComponent))]
        private void InstantiateAudioSource(in Entity entity, ref PBAudioSource sdkAudioSource, ref TransformComponent entityTransform)
        {
            // AudioSource audioSource = audioSourcesPool.Get();
            // audioSource.spatialBlend = 1;
            // audioSource.dopplerLevel = 0.1f;
            // audioSource.playOnAwake = false;
            //
            // audioSource.loop = sdkAudioSource.Loop;
            // audioSource.pitch = sdkAudioSource.Pitch;
            // audioSource.volume = sdkAudioSource.Volume;
            //
            // // if (sdkAudioSource.Playing && audioSource.clip != null)
            // //     audioSource.Play();
            //
            // TestAsyncLoading(sdkAudioSource, audioSource).Forget();
            //
            // var component = new AudioSourceComponent();
            // component.Result = audioSource;
            //
            // Transform rendererTransform = audioSource.transform;
            // rendererTransform.SetParent(entityTransform.Transform, false);
            // rendererTransform.ResetLocalTRS();
            //
            // World.Add(entity, component);
        }

        // private async UniTask TestAsyncLoading(PBAudioSource sdkAudioSource, AudioSource audioSource)
        // {
        //     if (!sceneData.TryGetContentUrl(sdkAudioSource.AudioClipUrl, out URLAddress audioClipUrl)) return;
        //
        //     var a =  await webRequestController.GetAudioClipAsync(
        //         new CommonArguments(audioClipUrl), new GetAudioClipArguments(sdkAudioSource.AudioClipUrl), ct: default(CancellationToken));
        //
        //     audioSource.clip = a.CreateAudioClip();
        //     audioSource.Play();
        // }
    }
}
