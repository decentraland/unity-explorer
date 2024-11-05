using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.Ipfs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Playground
{
    public class ScreenRecorderTester : MonoBehaviour
    {
        public string profileUrl = "https://peer-eu1.decentraland.org/lambdas/";

        public RectTransform canvasRectTransform;
        public ScreenshotHudView hud;

        [Space(5)]
        public Texture2D Texture;
        public ScreenshotMetadata metadata;

        private ScreenRecorder recorder;

        [ContextMenu(nameof(Screenshot))]
        public void Screenshot()
        {
            recorder ??= new ScreenRecorder(canvasRectTransform);
            hud.StartCoroutine(recorder.CaptureScreenshot(tex => Texture = tex));
        }

        [ContextMenu(nameof(CaptureMetadata))]
        public async Task CaptureMetadata()
        {
            Profile profile = await CreateProfile().ProfileAsync(default(CancellationToken));
            metadata = ScreenshotMetadataProcessor.Create(profile, null, Vector2Int.one, "Test Playground");
        }

        private SelfProfile CreateProfile()
        {
            var web3IdentityCache = new IWeb3IdentityCache.Default();

            var realmData = new RealmData(
                new LogIpfsRealm(
                    new IpfsRealm(
                        web3IdentityCache,
                        IWebRequestController.DEFAULT,
                        URLDomain.FromString("TestRealm"),
                        new ServerAbout(
                            lambdas: new ContentEndpoint(profileUrl)
                        )
                    )
                )
            );

            return new SelfProfile(
                new LogProfileRepository(
                    new RealmProfileRepository(IWebRequestController.DEFAULT, realmData, new DefaultProfileCache())
                ),
                web3IdentityCache,
                new EquippedWearables(),
                new WearableStorage(),
                new MemoryEmotesStorage(),
                new EquippedEmotes(),
                new List<string>(),
                null
            );
        }
    }
}
