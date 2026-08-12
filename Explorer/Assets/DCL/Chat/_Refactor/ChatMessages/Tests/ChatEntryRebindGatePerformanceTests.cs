using DCL.Chat;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.Translation;
using NUnit.Framework;

namespace DCL.Chat.ChatMessages.Tests
{
    /// <summary>
    /// EditMode checks for the two ChatEntryView bind change-gates. Neither touches a prefab, TMP,
    /// or a MonoBehaviour — they exercise the pure mechanisms the gates rest on, so they run in
    /// plain EditMode. The game singletons the ChatMessage constructor reaches (OfficialWalletsHelper
    /// -> FeatureFlagsConfiguration) are initialized in SetUp, mirroring the sibling
    /// ChatMessageReactionServiceShould, so building a message never NREs.
    /// </summary>
    [TestFixture]
    public class ChatEntryRebindGatePerformanceTests
    {
        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());
        }

        // The rebind gate skips the expensive path when ReferenceEquals(vm, last) AND
        // vm.Version == lastVersion — only safe if EVERY setter feeding a bound cell's visuals bumps
        // Version, else a mutated-but-same-Version view model would be gated as unchanged and strand
        // stale content.
        [Test]
        public void EveryRenderedSetter_BumpsVersion()
        {
            ChatMessageViewModel vm = ChatMessageViewModel.POOL.Get();

            try
            {
                int v = vm.Version;

                vm.Message = new ChatMessage("hello", "Alice", "0xsender", false, "#1234", 0d, false, true);
                Assert.Greater(vm.Version, v, "Message setter must bump Version"); v = vm.Version;

                vm.ShowDateDivider = true;
                Assert.Greater(vm.Version, v, "ShowDateDivider setter must bump Version"); v = vm.Version;

                vm.TranslationState = TranslationState.Success;
                Assert.Greater(vm.Version, v, "TranslationState setter must bump Version"); v = vm.Version;

                vm.TranslatedText = "translated";
                Assert.Greater(vm.Version, v, "TranslatedText setter must bump Version"); v = vm.Version;

                vm.Reactions = new ReactionSet();
                Assert.Greater(vm.Version, v, "Reactions setter must bump Version");
            }
            finally
            {
                ChatMessageViewModel.POOL.Release(vm);
            }
        }

        // A monotonic, never-reset Version closes the pooled-VM aliasing hole — after a view model is
        // released to the pool its Version must have advanced past any value a stale cell cached, so a
        // reacquired instance can never collide with a previously gated version.
        [Test]
        public void PoolRelease_AdvancesVersion()
        {
            ChatMessageViewModel vm = ChatMessageViewModel.POOL.Get();
            vm.Message = new ChatMessage("hello", "Alice", "0xsender", false, "#1234", 0d, false, true);

            int beforeRelease = vm.Version;
            ChatMessageViewModel.POOL.Release(vm);

            Assert.Greater(vm.Version, beforeRelease,
                "releasing a view model must advance Version so a reacquired instance cannot alias a gated cell");
        }

        // The profile-update callback re-renders the username only when the incoming profile differs
        // from what is CURRENTLY on screen (RenderedNameGate). A name -> A -> name round-trip must
        // re-render the final revert — the on-screen name is 'A', not the immutable message snapshot.
        [Test]
        public void RenderedNameGate_ReRendersRevertedName()
        {
            var gate = new RenderedNameGate();

            // Full bind rendered the message snapshot ("name").
            gate.SetRendered("name", "#1234", false);

            Assert.IsFalse(gate.ShouldRender("name", "#1234", false),
                "a profile matching what is already on screen must not re-render");

            Assert.IsTrue(gate.ShouldRender("A", "#1234", false),
                "a genuinely changed profile name must re-render");

            Assert.IsTrue(gate.ShouldRender("name", "#1234", false),
                "reverting to the original name from 'A' must re-render — the on-screen name is 'A', not 'name'");
        }

        // The gate also keys on wallet id and official flag, not name alone.
        [Test]
        public void RenderedNameGate_DetectsWalletAndOfficialChanges()
        {
            var gate = new RenderedNameGate();
            gate.SetRendered("name", "#1234", false);

            Assert.IsTrue(gate.ShouldRender("name", "#5678", false), "a changed wallet id must re-render");
            Assert.IsTrue(gate.ShouldRender("name", "#5678", true), "a changed official flag must re-render");
            Assert.IsFalse(gate.ShouldRender("name", "#5678", true), "an unchanged tuple must not re-render");
        }
    }
}
