using Arch.Core;
using DCL.SDKComponents.AudioSources;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips.Tests
{
    [TestFixture]
    public class LoadAudioClipSystemShould : LoadSystemBaseShould<LoadAudioClipSystem, AudioClip, GetAudioClipIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Audio/cuckoo-test-clip.mp3"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Audio/non_existing.mp3"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.mp3"}";

        protected override GetAudioClipIntention CreateSuccessIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments(successPath),
                AudioType = "a.mp3".ToAudioType(),
            };

        protected override GetAudioClipIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetAudioClipIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadAudioClipSystem CreateSystem() =>
            new (world, cache, TestSuite.TestWebRequestController.INSTANCE);

        public static LoadAudioClipSystem CreateSystem(World world) =>
            new (world, Substitute.For<IStreamableCache<AudioClip, GetAudioClipIntention>>(), TestSuite.TestWebRequestController.INSTANCE);

        protected override void AssertSuccess(AudioClip asset)
        {
            Assert.That(asset.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(asset.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(asset.ambisonic, Is.EqualTo(false));
            Assert.That(asset.channels, Is.EqualTo(2));
            Assert.That(asset.frequency, Is.EqualTo(44100));
            Assert.That(asset.loadInBackground, Is.EqualTo(false));
        }
    }
}
