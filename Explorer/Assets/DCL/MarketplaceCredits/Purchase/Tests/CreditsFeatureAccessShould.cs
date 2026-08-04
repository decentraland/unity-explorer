using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Web3;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine.TestTools;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class CreditsFeatureAccessShould
    {
        private const string ALLOWED_WALLET = "0xaabbccddeeff00112233445566778899aabbccdd";
        private const string OTHER_WALLET = "0x1111111111111111111111111111111111111111";

        private IWeb3Identity identity = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CancellationTokenSource warmUpCts = null!;

        [SetUp]
        public void SetUp()
        {
            warmUpCts = new CancellationTokenSource();

            identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(ALLOWED_WALLET));

            identityCache = Substitute.For<IWeb3IdentityCache>();
            identityCache.Identity.Returns(identity);
        }

        [TearDown]
        public void TearDown()
        {
            // Cancel before resetting the flags singleton: a constructor warm-up suspended on its
            // identity WaitUntil resumes only on a later player-loop tick, and without cancellation
            // it would wake up inside a subsequent test and crash on the reset singleton.
            warmUpCts.Cancel();
            warmUpCts.Dispose();

            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void AllowEveryoneWhenRestrictionFlagIsDisabled()
        {
            DisableAllFlags();

            Assert.IsTrue(NewAccess().IsUserAllowed());
        }

        [Test]
        public void AllowUserInAllowlist()
        {
            EnableWalletsFlag(walletsAllowlist: $"{OTHER_WALLET},{ALLOWED_WALLET}");

            Assert.IsTrue(NewAccess().IsUserAllowed());
        }

        [Test]
        public void RejectUserOutsideAllowlist()
        {
            EnableWalletsFlag(walletsAllowlist: OTHER_WALLET);

            Assert.IsFalse(NewAccess().IsUserAllowed());
        }

        [Test]
        public void AllowAnyUserWhenAllowlistIsEmpty()
        {
            EnableWalletsFlag(walletsAllowlist: null);

            Assert.IsTrue(NewAccess().IsUserAllowed());
        }

        [Test]
        public void MatchAllowlistWalletsCaseInsensitively()
        {
            EnableWalletsFlag(walletsAllowlist: ALLOWED_WALLET.ToUpperInvariant());

            Assert.IsTrue(NewAccess().IsUserAllowed());
        }

        [Test]
        public void FailClosedWhileIdentityIsUnknownWithoutCachingIt()
        {
            EnableWalletsFlag(walletsAllowlist: ALLOWED_WALLET);
            identityCache.Identity.Returns((IWeb3Identity?)null);
            CreditsFeatureAccess access = NewAccess();

            Assert.IsFalse(access.IsUserAllowed());

            // the pre-login verdict must not stick: once an identity appears, the real one is computed
            identityCache.Identity.Returns(identity);
            Assert.IsTrue(access.IsUserAllowed());
        }

        [Test]
        public void CacheVerdictUntilIdentityChanges()
        {
            EnableWalletsFlag(walletsAllowlist: ALLOWED_WALLET);
            CreditsFeatureAccess access = NewAccess();
            Assert.IsTrue(access.IsUserAllowed());

            // shrink the allowlist: the cached verdict must survive until the identity changes
            EnableWalletsFlag(walletsAllowlist: OTHER_WALLET);
            Assert.IsTrue(access.IsUserAllowed());

            identityCache.OnIdentityChanged += Raise.Event<Action>();
            Assert.IsFalse(access.IsUserAllowed());
        }

        [UnityTest]
        public IEnumerator ResolveAsyncVerdictOnceIdentityAppears() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableWalletsFlag(walletsAllowlist: ALLOWED_WALLET);
                identityCache.Identity.Returns((IWeb3Identity?)null);
                CreditsFeatureAccess access = NewAccess();

                UniTask<bool> verdict = access.IsUserAllowedToUseTheFeatureAsync(TimeoutToken());
                identityCache.Identity.Returns(identity);

                Assert.IsTrue(await verdict);
            });

        private CreditsFeatureAccess NewAccess() =>
            new (identityCache, warmUpCts.Token);

        private static void EnableWalletsFlag(string? walletsAllowlist)
        {
            FeatureFlagsResultDto dto = FeatureFlagsResultDto.Empty;
            dto.flags[FeatureFlagsStrings.CREDITS_WALLETS] = true;

            if (walletsAllowlist != null)
                dto.variants[FeatureFlagsStrings.CREDITS_WALLETS] = new FeatureFlagVariantDto
                {
                    name = FeatureFlagsStrings.WALLETS_VARIANT,
                    enabled = true,
                    payload = new FeatureFlagPayload { type = "string", value = walletsAllowlist },
                };

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(dto));
        }

        private static void DisableAllFlags()
        {
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
        }

        private static CancellationToken TimeoutToken() =>
            new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
    }
}
