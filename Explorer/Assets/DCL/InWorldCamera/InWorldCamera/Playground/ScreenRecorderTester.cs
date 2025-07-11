using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Ipfs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using DCL.Rendering.GPUInstancing;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.Playground
{
    public sealed class ScreenRecorderTester : MonoBehaviour
    {
        public string profileUrl = "https://peer-eu1.decentraland.org/lambdas/";

        public RectTransform canvasRectTransform;
        public ScreenshotHudDebug hud;

        [Space(5)]
        public Texture2D Texture;
        public ScreenshotMetadata metadata;

        private ScreenRecorder recorder;

        private void OnDestroy()
        {
            recorder?.Dispose();
        }

        [ContextMenu(nameof(Screenshot))]
        private IEnumerator Screenshot()
        {
            recorder ??= new ScreenRecorder(canvasRectTransform);

            StartCoroutine(recorder.CaptureScreenshot());
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;

            Texture = recorder.GetScreenshotAndReset();
            hud.Screenshot = Texture;
        }


        [ContextMenu(nameof(CaptureMetadata))]
        public async UniTask CaptureMetadata()
        {
            Profile? profile = await CreateProfile().ProfileAsync(CancellationToken.None);

            var builder = new ScreenshotMetadataBuilder(null, null, null, null);
            builder.FillMetadata(profile, null, Vector2Int.one, "Test Playground", "Test place id", Array.Empty<VisiblePerson>());
            metadata = builder.GetMetadataAndReset();
            hud.Metadata = metadata;
        }

        private SelfProfile CreateProfile()
        {
            var web3IdentityCache = new IWeb3IdentityCache.Default();

            var realmData = new RealmData(
                new LogIpfsRealm(
                    new IpfsRealm(
                        web3IdentityCache,
                        IWebRequestController.UNITY,
                        URLDomain.FromString("TestRealm"),
                        null!,
                        new ServerAbout(
                            lambdas: new ContentEndpoint(profileUrl)
                        )
                    )
                )
            );

            var world = World.Create();
            Entity playerEntity = world.Create();

            return new SelfProfile(
                new LogProfileRepository(
                    new RealmProfileRepository(IWebRequestController.UNITY, realmData, new DefaultProfileCache())
                ),
                web3IdentityCache,
                new EquippedWearables(),
                new WearableStorage(),
                new MemoryEmotesStorage(),
                new EquippedEmotes(),
                new List<string>(),
                null,
                new DefaultProfileCache(),
                world,
                playerEntity
            );
        }
    }
}
