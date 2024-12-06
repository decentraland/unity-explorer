using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestStressTestUtility
    {
        private static readonly URLAddress SUCCESS = URLAddress.FromString("https://res.soulmagic.online/v093/ui_atlas_5.png");
        private static readonly URLAddress FAIL = URLAddress.FromString("https://ab-cdn.decentraland.org/LOD/1/bafkreibkkn6xli3w7dhfk6adaj3bi2xa5a4lk35anv2hwx6bddnjcpbbzi_1_mac");

        private readonly IWebRequestController webRequestController;

        public WebRequestStressTestUtility(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask StartConcurrentAsync(int count, int retriesCount, bool failed, float delay)
        {
            bool hasDelay = Mathf.Approximately(delay, 0);

            var tasks = new UniTask[count];

            for (var i = 0; i < count; i++)
            {
                tasks[i] = StartRequestAsync(i, retriesCount, failed);

                if (hasDelay)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime);
            }

            await UniTask.WhenAll(tasks);
        }

        public async UniTask StartSequentialAsync(int count, int retriesCount, bool failed, float delay)
        {
            bool hasDelay = Mathf.Approximately(delay, 0);

            for (var i = 0; i < count; i++)
            {
                await StartRequestAsync(i, retriesCount, failed);

                if (hasDelay)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime);
            }
        }

        private async UniTask StartRequestAsync(int requestNumber, int retriesCount, bool failed)
        {
            try
            {
                if (failed)

                    // texture
                    await webRequestController.GetTextureAsync(
                        new CommonArguments(FAIL, attemptsCount: retriesCount),
                        new GetTextureArguments(TextureType.Albedo),
                        new GetTextureWebRequest.CreateTextureOp(TextureWrapMode.Clamp, FilterMode.Bilinear),
                        CancellationToken.None,
                        reportData: ReportCategory.DEBUG
                    );
                else

                    // binary data
                    await webRequestController.GetAsync(
                                                   new CommonArguments(SUCCESS, attemptsCount: retriesCount),
                                                   CancellationToken.None,
                                                   reportData: ReportCategory.DEBUG
                                               )
                                              .WithNoOpAsync();

                ReportHub.Log(ReportCategory.DEBUG, $"Request #{requestNumber} successfully completed");
            }
            catch (Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"Request #{requestNumber} failed: {e.Message}"); }
        }
    }
}
