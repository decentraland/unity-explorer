using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.AssetBundles
{
    public static class AssetBundleRegistryVersionHelper
    {
        private static readonly IExtendedObjectPool<StringBuilder> STRING_BUILDER_POOL = new ExtendedObjectPool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear(), defaultCapacity: 2);
        private static readonly ThreadSafeListPool<ABVersionsResponse> DTO_POOL = new (25, 50);

        public static async UniTask<AssetBundlesVersions> GetABRegistryVersionsByPointersAsync(
            URN[] pointers,
            IWebRequestController webRequestController,
            string assetBundleRegistryVersionURL,
            ReportData reportCategory,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            using PooledObject<URLBuilder> scope = DecentralandUrlsUtils.BuildFromDomain(assetBundleRegistryVersionURL, out URLBuilder urlBuilder);
            using var sbScope = STRING_BUILDER_POOL.Get(out var bodyBuilder);

            bodyBuilder.Append("{\"pointers\":[");

            for (int i = 0; i < pointers.Length; ++i)
            {
                bodyBuilder.Append('\"');

                bodyBuilder.Append(pointers[i].LowerCaseUrn());
                bodyBuilder.Append('\"');

                if (i != pointers.Length - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            var url = urlBuilder.Build();
            using var dtoPooledList = DTO_POOL.AutoScope();

            var result = AssetBundlesVersions.Create();

            try
            {
                await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct, reportCategory)
                    .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

                foreach (var element in dtoPooledList.Value)
                {
                    if (element.pointers == null || element.pointers.Length == 0)
                    {
                        ReportHub.LogError(reportCategory, "Asset bundle registry returned an entry with no pointers. Skipping.");
                        continue;
                    }

                    var ab = element.versions.assets;

                    if (!ab.webgl.HasValue || !ab.mac.HasValue || !ab.windows.HasValue)
                    {
                        ReportHub.LogError(reportCategory, $"Asset bundle registry did not return all platform versions for pointer {element.pointers[0]}. Registry must provide mac, windows and webgl. Skipping.");
                        continue;
                    }

                    result.versions.Add(element.pointers[0], new AssetBundlesVersions.PlatformVersionInfo
                    {
                        mac = new AssetBundlesVersions.VersionInfo { version = ab.mac.Value.version, buildDate = ab.mac.Value.buildDate },
                        windows = new AssetBundlesVersions.VersionInfo { version = ab.windows.Value.version, buildDate = ab.windows.Value.buildDate },
                        webgl = new AssetBundlesVersions.VersionInfo { version = ab.webgl.Value.version, buildDate = ab.webgl.Value.buildDate },
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, reportCategory);
                return result;
            }

            return result;
        }
    }
}
