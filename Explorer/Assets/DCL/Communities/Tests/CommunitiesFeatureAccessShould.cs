using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Web3;
using DCL.Web3.Identities;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine.TestTools;

namespace DCL.Communities.Tests
{
    public class CommunitiesFeatureAccessShould
    {
        private const string ALLOWED_WALLET = "0xaabbccddeeff00112233445566778899aabbccdd";
        private const string OTHER_WALLET = "0x1111111111111111111111111111111111111111";

        private IWeb3IdentityCache identityCache = null!;
        private CancellationTokenSource warmUpCts = null!;

        [SetUp]
        public void SetUp()
        {
            CommunitiesFeatureAccess.disableForTests = true;
            warmUpCts = new CancellationTokenSource();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
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

            CommunitiesFeatureAccess.disableForTests = false;
            FeatureFlagsConfiguration.Reset();
        }

        [UnityTest]
        public IEnumerator PopulateCacheInBackgroundOnConstruction() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: ALLOWED_WALLET);

                CommunitiesFeatureAccess access = NewAccess();

                // the cached verdict must appear without any explicit call to the async check:
                // only the constructor warm-up runs here
                await UniTask.WaitUntil(() => access.IsUserAllowedCached(), cancellationToken: TimeoutToken());

                Assert.IsTrue(access.IsUserAllowedCached());
            });

        [UnityTest]
        public IEnumerator AllowUserInAllowlist() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: $"{OTHER_WALLET},{ALLOWED_WALLET}");

                Assert.IsTrue(await NewAccess().IsUserAllowedToUseTheFeatureAsync(CancellationToken.None));
            });

        [UnityTest]
        public IEnumerator RejectUserOutsideAllowlist() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: OTHER_WALLET);

                Assert.IsFalse(await NewAccess().IsUserAllowedToUseTheFeatureAsync(CancellationToken.None));
            });

        [UnityTest]
        public IEnumerator AllowAnyUserWhenAllowlistIsEmpty() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: null);

                Assert.IsTrue(await NewAccess().IsUserAllowedToUseTheFeatureAsync(CancellationToken.None));
            });

        [UnityTest]
        public IEnumerator RejectAnyUserWhenFlagIsDisabled() =>
            UniTask.ToCoroutine(async () =>
            {
                DisableAllFlags();

                Assert.IsFalse(await NewAccess().IsUserAllowedToUseTheFeatureAsync(CancellationToken.None));
            });

        [UnityTest]
        public IEnumerator MatchAllowlistWalletsCaseInsensitively() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: ALLOWED_WALLET.ToUpperInvariant());

                Assert.IsTrue(await NewAccess().IsUserAllowedToUseTheFeatureAsync(CancellationToken.None));
            });

        [Test]
        public void ReportEnabledFlagFromFlagOnlyCheck()
        {
            // the allowlist does not include the current user: the flag-only check must ignore it
            EnableCommunitiesFlag(walletsAllowlist: OTHER_WALLET);

            Assert.IsTrue(NewAccess().IsFeatureEnabled());
        }

        [Test]
        public void ReportDisabledFlagFromFlagOnlyCheck()
        {
            DisableAllFlags();

            Assert.IsFalse(NewAccess().IsFeatureEnabled());
        }

        [Test]
        public void NotLetFlagOnlyCheckTouchTheAllowlistCache()
        {
            // the flag is on but the current user is outside the allowlist
            EnableCommunitiesFlag(walletsAllowlist: OTHER_WALLET);
            CommunitiesFeatureAccess access = NewAccess();

            Assert.IsTrue(access.IsFeatureEnabled());
            Assert.IsFalse(access.IsUserAllowedCached());
        }

        [UnityTest]
        public IEnumerator ResetCacheWhenIdentityChanges() =>
            UniTask.ToCoroutine(async () =>
            {
                EnableCommunitiesFlag(walletsAllowlist: ALLOWED_WALLET);
                CommunitiesFeatureAccess access = NewAccess();
                await UniTask.WaitUntil(() => access.IsUserAllowedCached(), cancellationToken: TimeoutToken());

                identityCache.OnIdentityChanged += Raise.Event<Action>();

                Assert.IsFalse(access.IsUserAllowedCached());
            });

        private CommunitiesFeatureAccess NewAccess() =>
            new (identityCache, Substitute.For<IAppArgs>(), warmUpCts.Token);

        private static void EnableCommunitiesFlag(string? walletsAllowlist)
        {
            FeatureFlagsResultDto dto = FeatureFlagsResultDto.Empty;
            dto.flags[FeatureFlagsStrings.COMMUNITIES] = true;

            if (walletsAllowlist != null)
                dto.variants[FeatureFlagsStrings.COMMUNITIES] = new FeatureFlagVariantDto
                {
                    name = FeatureFlagsStrings.WALLETS_VARIANT,
                    enabled = true,
                    payload = new FeatureFlagPayload { type = "string", value = walletsAllowlist },
                };

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(dto));
        }

        private static void DisableAllFlags() =>
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

        private static CancellationToken TimeoutToken() =>
            new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
    }
}
