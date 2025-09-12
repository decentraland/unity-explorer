using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic.LaunchModes;
using Global.Dynamic.RealmUrl.Names;
using System;
using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public class RealmUrls
    {
        private readonly RealmLaunchSettings realmLaunchSettings;
        private readonly IRealmNamesMap realmNames;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public RealmUrls(RealmLaunchSettings realmLaunchSettings, IRealmNamesMap realmNames, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.realmLaunchSettings = realmLaunchSettings;
            this.realmNames = realmNames;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<string> StartingRealmAsync(CancellationToken ct)
        {
            return realmLaunchSettings.initialRealm switch
                   {
                       InitialRealm.GenesisCity => decentralandUrlsSource.Url(DecentralandUrl.Genesis),
                       InitialRealm.SDK => IRealmNavigator.SDK_TEST_SCENES_URL,
                       InitialRealm.Goerli => IRealmNavigator.GOERLI_URL,
                       InitialRealm.StreamingWorld => IRealmNavigator.STREAM_WORLD_URL,
                       InitialRealm.TestScenes => IRealmNavigator.TEST_SCENES_URL,
                       InitialRealm.World => decentralandUrlsSource.Url(DecentralandUrl.WorldContentServer) + "/" + realmLaunchSettings.TargetWorld,
                       InitialRealm.Localhost => IRealmNavigator.LOCALHOST,
                       InitialRealm.Custom => await CustomRealmAsync(ct),
                       _ => decentralandUrlsSource.Url(DecentralandUrl.Genesis),
                   };
        }

        public async UniTask<string?> LocalSceneDevelopmentRealmAsync(CancellationToken ct) =>
            realmLaunchSettings.CurrentMode switch
            {
                LaunchMode.Play => null,
                LaunchMode.LocalSceneDevelopment => await StartingRealmAsync(ct),
                _ => throw new ArgumentOutOfRangeException()
            };

        private async UniTask<string> CustomRealmAsync(CancellationToken ct)
        {
            string realm = realmLaunchSettings.customRealm;

            if (realm.StartsWith("http://", StringComparison.Ordinal) || realm.StartsWith("https://", StringComparison.Ordinal))
                return realm;

            return await realmNames.UrlFromNameAsync(realm, ct);
        }
    }
}
