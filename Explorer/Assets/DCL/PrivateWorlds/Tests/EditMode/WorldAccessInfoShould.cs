using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.PrivateWorlds.Tests.EditMode
{
    [TestFixture]
    public class WorldAccessInfoShould
    {
        [Test]
        public void ParseUnrestricted_WhenAccessIsNull()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions { Access = null }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.Unrestricted, info.AccessType);
            Assert.AreEqual("0xOwner", info.OwnerAddress);
        }

        [Test]
        public void ParseUnrestricted_WhenTypeIsEmptyString()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.Unrestricted, info.AccessType);
        }

        [Test]
        public void ParseUnrestricted_CaseInsensitive()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "UNRESTRICTED" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.Unrestricted, info.AccessType);
        }

        [Test]
        public void ParseSharedSecret()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "shared-secret" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.SharedSecret, info.AccessType);
        }

        [Test]
        public void ParseSharedSecret_CaseInsensitive()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "Shared-Secret" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.SharedSecret, info.AccessType);
        }

        [Test]
        public void ParseAllowList_WithWalletsAndCommunities()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig
                    {
                        Type = "allow-list",
                        Wallets = new List<string> { "0xAlice", "0xBob" },
                        Communities = new List<string> { "community-1", "community-2" }
                    }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.AllowList, info.AccessType);
            Assert.AreEqual(2, info.AllowedWallets.Count);
            Assert.Contains("0xAlice", info.AllowedWallets);
            Assert.Contains("0xBob", info.AllowedWallets);
            Assert.AreEqual(2, info.AllowedCommunities.Count);
            Assert.Contains("community-1", info.AllowedCommunities);
        }

        [Test]
        public void ParseAllowList_WithNullWalletsAndCommunities()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig
                    {
                        Type = "allow-list",
                        Wallets = null,
                        Communities = null
                    }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.AllowList, info.AccessType);
            Assert.IsNotNull(info.AllowedWallets);
            Assert.AreEqual(0, info.AllowedWallets.Count);
            Assert.IsNotNull(info.AllowedCommunities);
            Assert.AreEqual(0, info.AllowedCommunities.Count);
        }

        [Test]
        public void ParseAllowList_CaseInsensitive()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "Allow-List" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.AllowList, info.AccessType);
        }

        [Test]
        public void DefaultToUnrestricted_WhenTypeIsUnknown()
        {
            // Unknown type falls through all if/else branches — AccessType stays at default (Unrestricted)
            var response = new WorldPermissionsResponse
            {
                Owner = "0xOwner",
                Permissions = new WorldPermissions
                {
                    Access = new AccessPermissionConfig { Type = "some-future-type" }
                }
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual(WorldAccessType.Unrestricted, info.AccessType);
        }

        [Test]
        public void PreserveOwnerAddress()
        {
            var response = new WorldPermissionsResponse
            {
                Owner = "0xDeadBeef",
                Permissions = new WorldPermissions()
            };

            var info = WorldAccessInfo.FromResponse(response);

            Assert.AreEqual("0xDeadBeef", info.OwnerAddress);
        }

        [Test]
        public void ReturnTrue_WhenWalletIsInList()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string> { "0xAlice", "0xBob" }
            };

            Assert.IsTrue(info.IsWalletAllowed("0xAlice"));
        }

        [Test]
        public void ReturnTrue_CaseInsensitive()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string> { "0xaAbBcC" }
            };

            Assert.IsTrue(info.IsWalletAllowed("0xAABBCC"));
        }

        [Test]
        public void ReturnFalse_WhenWalletNotInList()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string> { "0xAlice" }
            };

            Assert.IsFalse(info.IsWalletAllowed("0xCharlie"));
        }

        [Test]
        public void ReturnFalse_WhenListIsEmpty()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string>()
            };

            Assert.IsFalse(info.IsWalletAllowed("0xAlice"));
        }

        [Test]
        public void ReturnFalse_WhenWalletIsNull()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string> { "0xAlice" }
            };

            Assert.IsFalse(info.IsWalletAllowed(null!));
        }

        [Test]
        public void ReturnFalse_WhenWalletIsEmpty()
        {
            var info = new WorldAccessInfo
            {
                AllowedWallets = new List<string> { "0xAlice" }
            };

            Assert.IsFalse(info.IsWalletAllowed(""));
        }
    }
}
