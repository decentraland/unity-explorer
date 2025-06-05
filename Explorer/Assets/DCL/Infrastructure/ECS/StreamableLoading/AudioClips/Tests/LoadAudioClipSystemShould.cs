﻿using Arch.Core;
using DCL.SDKComponents.AudioSources;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips.Tests
{
    [TestFixture(WebRequestsMode.UNITY)]
    public class LoadAudioClipSystemShould : LoadSystemBaseShould<LoadAudioClipSystem, AudioClipData, GetAudioClipIntention>
    {
        public LoadAudioClipSystemShould(WebRequestsMode webRequestsMode) : base(webRequestsMode) { }

        private Uri successPath => new ($"file://{Application.dataPath + "/../TestResources/Audio/cuckoo-test-clip.mp3"}");
        private Uri failPath => new ($"file://{Application.dataPath + "/../TestResources/Audio/non_existing.mp3"}");
        private Uri wrongTypePath => new ($"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.mp3"}");

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

        protected override LoadAudioClipSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, cache, webRequestController);

        public static LoadAudioClipSystem CreateSystem(World world, IWebRequestController webRequestController) =>
            new (world, Substitute.For<IStreamableCache<AudioClipData, GetAudioClipIntention>>(), webRequestController);

        protected override void AssertSuccess(AudioClipData data)
        {
            AudioClip asset = data.Asset;

            Assert.That(asset.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(asset.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(asset.ambisonic, Is.EqualTo(false));
            Assert.That(asset.channels, Is.EqualTo(2));
            Assert.That(asset.frequency, Is.EqualTo(44100));
            Assert.That(asset.loadInBackground, Is.EqualTo(false));
        }
    }
}
