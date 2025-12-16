using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    public static class AssetBundleRegistryVersionHelper
    {
        private static readonly IExtendedObjectPool<URLBuilder> URL_BUILDER_POOL = new ExtendedObjectPool<URLBuilder>(() => new URLBuilder(), ub => ub.Clear() , defaultCapacity: 2);
        private static readonly IExtendedObjectPool<StringBuilder> STRING_BUILDER_POOL = new ExtendedObjectPool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear(), defaultCapacity: 2);
        private static readonly ThreadSafeListPool<ABVersionsResponse> DTO_POOL = new (25, 50);

        public static async UniTask<AssetBundlesVersions> GetABRegistryVersionsByPointersAsync(
            URN[] pointers,
            IWebRequestController webRequestController,
            URLDomain assetBundleRegistryVersionURL,
            ReportData reportCategory,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            using var scope = URL_BUILDER_POOL.Get(out var urlBuilder);
            using var sbScope = STRING_BUILDER_POOL.Get(out var bodyBuilder);

            urlBuilder.AppendDomain(assetBundleRegistryVersionURL);

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

            URLAddress url = urlBuilder.Build();
            using PoolExtensions.Scope<List<ABVersionsResponse>> dtoPooledList = DTO_POOL.AutoScope();

            AssetBundlesVersions result = AssetBundlesVersions.Create();

            try
            {
                await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct, reportCategory)
                                          .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, reportCategory);
                return result;
            }

            foreach (var element in dtoPooledList.Value)
                result.versions.Add(element.pointers[0], new AssetBundlesVersions.PlatformVersionInfo
                {
                    mac = new AssetBundlesVersions.VersionInfo
                    {
                        version = element.versions.assets.mac.version,
                        buildDate = element.versions.assets.mac.buildDate,
                    },
                    windows = new AssetBundlesVersions.VersionInfo
                    {
                        version = element.versions.assets.windows.version,
                        buildDate = element.versions.assets.windows.buildDate,
                    }
                });

            return result;
        }
    }
}
