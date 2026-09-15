using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.ExplorePanel.Lobby.Tests
{
    public class MenuRealmNavigatorShould
    {
        [Test, Timeout(10000)]
        public async Task DeferTravelUntilWorldPreparationCompletes()
        {
            var status = new LoadingStatus();
            var navigator = Substitute.For<IRealmNavigator>();
            using var menu = new MenuRealmNavigator(navigator, status, new ILoadingScreen.EmptyLoadingScreen());
            UniTask<EnumResult<TaskError>> pending = menu.TeleportToParcelAsync(Vector2Int.one, CancellationToken.None, false, true);
            Assert.That(navigator.ReceivedCalls(), Is.Empty);

            status.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
            await pending;
            _ = navigator.Received(1).TeleportToParcelAsync(Vector2Int.one, Arg.Any<CancellationToken>(), false, true);
        }

        [TestCase(LoadingStatus.LoadingStage.Failed, TaskError.MessageError)]
        [TestCase(LoadingStatus.LoadingStage.Cancelled, TaskError.Cancelled)]
        [Timeout(10000)]
        public async Task EndPendingTravelBeforeRecoveryStarts(LoadingStatus.LoadingStage stage, TaskError expected)
        {
            var status = new LoadingStatus();
            var navigator = Substitute.For<IRealmNavigator>();
            using var menu = new MenuRealmNavigator(navigator, status, new ILoadingScreen.EmptyLoadingScreen());
            UniTask<EnumResult<TaskError>> pending = menu.TeleportToParcelAsync(Vector2Int.one, CancellationToken.None, false);
            status.SetCurrentStage(stage);
            UniTask<EnumResult<ChangeRealmError>> subsequent = menu.TryChangeRealmAsync(URLDomain.FromString("https://world.example"), CancellationToken.None);
            status.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            Assert.That((await pending).Error?.State, Is.EqualTo(expected));
            Assert.That((await subsequent).Error?.State, Is.EqualTo(expected.AsChangeRealmError()));
            Assert.That(navigator.ReceivedCalls(), Is.Empty);
        }
    }
}
