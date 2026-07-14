using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.Tests.Editor
{
    public class DecentralandIdentityShould
    {
        private const string ADDRESS = "0x0000000000000000000000000000000000000001";

        private static DecentralandIdentity NewIdentity()
        {
            AuthChain authChain = AuthChain.Create();
            authChain.SetSigner(ADDRESS);

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = "ephemeral payload",
                signature = "0xephemeralsignature",
            });

            IWeb3Account ephemeralAccount = Substitute.For<IWeb3Account>();
            ephemeralAccount.Sign(Arg.Any<string>()).Returns("0xentitysignature");

            return new DecentralandIdentity(new Web3Address(ADDRESS), ephemeralAccount, DateTime.UtcNow.AddDays(1), authChain, IWeb3Identity.Web3IdentitySource.None);
        }

        [Test]
        public void SignEntityWhileAlive()
        {
            // Arrange
            DecentralandIdentity identity = NewIdentity();

            // Act
            AuthChain signed = identity.Sign("entityId");

            // Assert
            Assert.IsTrue(signed.TryGet(AuthLinkType.ECDSA_SIGNED_ENTITY, out AuthLink link));
            Assert.AreEqual("entityId", link.payload);

            signed.Dispose();
            identity.Dispose();
        }

        [Test]
        public void ThrowObjectDisposedOnSignAfterDispose()
        {
            // Arrange
            DecentralandIdentity identity = NewIdentity();

            // Act
            identity.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => identity.Sign("entityId"));
        }

        [Test]
        public void ThrowObjectDisposedEvenAfterPoolReissuesItsAuthChain()
        {
            // Arrange
            DecentralandIdentity identity = NewIdentity();
            identity.Dispose();

            // Act - the pool can re-issue the identity's released AuthChain instance with the
            // disposed flag reset; the identity guard must not depend on that pooled state.
            AuthChain reissued = AuthChain.Create();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => identity.Sign("entityId"));

            reissued.Dispose();
        }
    }
}
