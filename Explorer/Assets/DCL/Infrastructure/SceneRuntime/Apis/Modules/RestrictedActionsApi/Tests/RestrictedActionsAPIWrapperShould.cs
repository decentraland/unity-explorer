using NSubstitute;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.SceneRuntime.Apis.RestrictedActionsApi.Tests
{
    public class RestrictedActionsAPIWrapperShould
    {
        private const string TEST_REALM = "TestRealm";
        private static readonly Vector2Int TEST_PARCEL = new (10, 20);

        private IRestrictedActionsAPI api = null!;
        private RestrictedActionsAPIWrapper wrapper = null!;

        [SetUp]
        public void SetUp()
        {
            api = Substitute.For<IRestrictedActionsAPI>();
            wrapper = new RestrictedActionsAPIWrapper(api, new CancellationTokenSource());
        }

        [TestCase(null)]
        [TestCase("")]
        public void IgnoreTeleportWithNeitherCoordinatesNorRealm(string? realm)
        {
            // Act
            LogAssert.Expect(LogType.Warning, new Regex("TeleportTo"));
            wrapper.TeleportTo(false, 0, 0, realm);

            // Assert
            api.DidNotReceive().TryTeleportTo(Arg.Any<TeleportDestination>());
        }

        [Test]
        public void ForwardParcelDestination()
        {
            // Act
            wrapper.TeleportTo(true, TEST_PARCEL.x, TEST_PARCEL.y, null);

            // Assert
            api.Received(1).TryTeleportTo(TeleportDestination.FromParcel(TEST_PARCEL));
        }

        [Test]
        public void ForwardRealmDestinationWithParcel()
        {
            // Act
            wrapper.TeleportTo(true, TEST_PARCEL.x, TEST_PARCEL.y, TEST_REALM);

            // Assert
            api.Received(1).TryTeleportTo(TeleportDestination.FromRealm(new RealmDestination(TEST_REALM, TEST_PARCEL)));
        }

        [Test]
        public void ForwardRealmDestinationWithoutParcel()
        {
            // Act
            wrapper.TeleportTo(false, 0, 0, TEST_REALM);

            // Assert
            api.Received(1).TryTeleportTo(TeleportDestination.FromRealm(new RealmDestination(TEST_REALM, null)));
        }
    }
}
