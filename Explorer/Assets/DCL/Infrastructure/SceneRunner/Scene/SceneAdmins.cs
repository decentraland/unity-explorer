using System.Collections.Generic;
using DCL.WebRequests;
using ECS;
using SceneRunner.Scene;
using Utility.Multithreading;
using System;
using System.Threading;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;

namespace SceneRunner.Admins
{
    /// <summary>
    ///     Exists per scene
    /// </summary>
    public class SceneAdmins
    {
        public enum Status
        {
            Idle,
            Loading,
            Loaded,
            Error
        }

        public struct AdminInfo
        {
            public string id;
            public string name;
            public string admin;
            public string active;
            public bool canBeRemoved;
        }

        // BEGIN Copied from Explorer\Assets\DCL\Tests\PlayMode\PerformanceTests\GatekeeperPerformanceTests.cs
        [Serializable]
        public class RealmMetadata
        {
            public string hostname;
            public string protocol;
            public string serverName;
        }

        [Serializable]
        public class RequestMetadata
        {
            public string signer;
            public RealmMetadata realm;
            public string sceneId;
            public string parcel;
        }
        // END Copied

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urls;
        private readonly IRealmData realmData;
        private readonly ISceneData sceneData;

        private readonly SemaphoreSlim operationLock = new (initialCount: 1, maxCount: 1);
        private readonly ConcurrentDictionary<string, AdminInfo> wallets = new (StringComparer.OrdinalIgnoreCase);
        private Status status = Status.Idle;

        public SceneAdmins(
                IWebRequestController webRequestController,
                IDecentralandUrlsSource urls,
                IRealmData realmData,
                ISceneData sceneData
                )
        {
            this.webRequestController = webRequestController;
            this.urls = urls;
            this.realmData = realmData;
            this.sceneData = sceneData;
        }

#if SCENE_ADMINS_TESTS
        public static SceneAdmins NewTestInstance()
        {
            return new SceneAdmins(null!, null!, null!, null!);
        }
#endif

        public async UniTaskVoid FireRequestAsync(CancellationToken ct)
        {
#if !SCENE_ADMINS_TESTS // it's not required to execute an actual request for tests

            using var _ = await operationLock.LockAsync();

            if (status is Status.Loading)
            {
                ReportHub.LogWarning(ReportCategory.SCENE_ADMINS, "Attempt to fire twice. SceneAdmins loading is already in progress");
                return;
            }

            status = Status.Loading;

            try
            {
                RealmMetadata realmMetadata = new RealmMetadata()
                {
                    hostname = realmData.Hostname,
                             protocol = realmData.Protocol,
                             serverName = realmData.RealmName,
                };

                Vector2Int baseParcel = sceneData.SceneShortInfo.BaseParcel;

                RequestMetadata metadata = new RequestMetadata()
                {
                    signer = "decentraland-kernel-scene",
                           realm = realmMetadata,
                           sceneId = sceneData.SceneEntityDefinition.id!,
                           parcel =  $"{baseParcel.x},{baseParcel.y}", // TODO We should actually introduce Parcel type to encapsule that format
                };

                string json = JsonUtility.ToJson(metadata);
                string url = urls.Url(DecentralandUrl.SceneAdmins);

                var list = await webRequestController.SignedFetchGetAsync(url, json, ct)
                    .CreateFromJson<List<AdminInfo>>(WRJsonParser.Newtonsoft);

                wallets.Clear();
                foreach (AdminInfo r in list) 
                {
                    wallets[r.admin] = r;
                }

                status = Status.Loaded;
            }
            catch (OperationCanceledException) 
            { 
                status = Status.Idle;
            }
            catch (Exception e)
            {
                status = Status.Error;
                ReportHub.LogException(e, ReportCategory.SCENE_ADMINS);
            }
#endif
        }

        // Null if not loaded yet
        public bool? IsAdmin(string wallet)
        {
#if SCENE_ADMINS_TESTS
            return true; // consider always an admin during tests
#else
            if (status == Status.Loaded)
            {
                return wallets.ContainsKey(wallet);
            }
            else
            {
                return null;
            }
#endif
        }

        public Result<IReadOnlyDictionary<string, AdminInfo>> CurrentAdmins()
        {
            if (status == Status.Loaded)
            {
                return Result<IReadOnlyDictionary<string, AdminInfo>>.SuccessResult(wallets);
            }
            else
            {
                return Result<IReadOnlyDictionary<string, AdminInfo>>.ErrorResult($"Cannot provide admins. Current status: {status}");
            }
        }
    }
}
