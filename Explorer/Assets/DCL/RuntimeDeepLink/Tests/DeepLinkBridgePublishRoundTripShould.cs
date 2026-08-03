using DCL.Utility.Types;
using Global.AppArgs;
using NUnit.Framework;
using System.IO;
using System.Reflection;

namespace DCL.RuntimeDeepLink.Tests
{
    /// <summary>
    ///     Regression coverage for single-instance deeplink forwarding (unity-explorer#9520 — the Mac Intel
    ///     MetaMask sign-in loop: a second launch carrying the browser's <c>decentraland://open?signin=...</c>
    ///     callback trips the "Only a single instance of Decentraland is allowed to run" guard and the signin id
    ///     is discarded, stranding the user on the sign-in screen).
    ///     <para>
    ///     The fix has the guarded second instance publish its raw deeplink to the same bridge file
    ///     <see cref="DeepLinkSentinel" />'s poller already reads (<c>DeepLinkSentinel.TryPublishToBridge</c>),
    ///     then exit silently instead of popping up. This is the write half of that round trip: publish via the
    ///     new API, then feed the resulting file straight into the exact parse call the poller itself makes
    ///     (<see cref="DeepLink.FromJson" />) and assert the DTO comes back with the same signin/authRequestId.
    ///     </para>
    ///     <para>
    ///     <c>TryPublishToBridge</c> is new production API added by the fix, so it is looked up and invoked
    ///     through reflection: on a pre-fix tree the lookup finds nothing and the test fails on an explicit
    ///     "publish path missing" assertion instead of a compile error, keeping this file buildable against both
    ///     trees (same reflection-shim idiom as <c>McpHttpServerShould</c> for production statics tests must not
    ///     reference directly).
    ///     </para>
    /// </summary>
    public class DeepLinkBridgePublishRoundTripShould
    {
        private const string PUBLISH_METHOD_NAME = "TryPublishToBridge";
        private const string BRIDGE_PATH_FIELD_NAME = "DEEP_LINK_BRIDGE_PATH";

        private const string SIGNIN_ID = "4df293cd-52d1-4e8a-9c3a-signinlooptest";
        private const string AUTH_REQUEST_ID = "9c1d2e3f-authrequest-signinlooptest";
        private const string RAW_SIGNIN_DEEPLINK = "decentraland://open?signin=" + SIGNIN_ID + "&authRequestId=" + AUTH_REQUEST_ID;

        // Null on a pre-fix tree (method does not exist yet) — every [Test] below asserts on this first so the
        // failure is an explicit, readable assertion rather than a NullReferenceException.
        private static readonly MethodInfo? PUBLISH_METHOD =
            typeof(DeepLinkSentinel).GetMethod(PUBLISH_METHOD_NAME, BindingFlags.Public | BindingFlags.Static);

        private string bridgePath = null!;
        private string tempPath = null!;
        private bool hadPreExistingFile;
        private string? preExistingContent;

        [SetUp]
        public void SetUp()
        {
            // DEEP_LINK_BRIDGE_PATH already exists pre-fix (it is what the poller reads today), so this lookup
            // never fails on either tree — reflection is used purely to avoid hard-coding the platform-specific
            // path a second time.
            FieldInfo? pathField = typeof(DeepLinkSentinel).GetField(BRIDGE_PATH_FIELD_NAME, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(pathField, Is.Not.Null, $"DeepLinkSentinel.{BRIDGE_PATH_FIELD_NAME} not found — the sentinel's own bridge-file path constant is missing");

            bridgePath = (string) pathField!.GetValue(null)!;
            tempPath = bridgePath + ".tmp";

            // Never clobber a real pending bridge file this machine may already have (e.g. an unconsumed
            // launcher-placed signin); back it up and restore it in TearDown no matter what the test does.
            hadPreExistingFile = File.Exists(bridgePath);
            if (hadPreExistingFile)
                preExistingContent = File.ReadAllText(bridgePath);

            Directory.CreateDirectory(Path.GetDirectoryName(bridgePath)!);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            if (hadPreExistingFile)
                File.WriteAllText(bridgePath, preExistingContent!);
            else if (File.Exists(bridgePath))
                File.Delete(bridgePath);
        }

        [Test]
        public void PublishASigninDeepLinkThatTheSentinelParsesBackIdentically()
        {
            Assert.That(PUBLISH_METHOD, Is.Not.Null,
                $"publish path missing: DeepLinkSentinel.{PUBLISH_METHOD_NAME} does not exist on this tree — " +
                "the single-instance deeplink-forwarding fix for the Mac Intel MetaMask sign-in loop " +
                "(unity-explorer#9520) is absent, so a second-instance signin deeplink still has no way to " +
                "reach the first instance's DeepLinkSentinel poller.");

            if (hadPreExistingFile)
                File.Delete(bridgePath); // the publish call itself must run against a clean slate

            var published = (bool) PUBLISH_METHOD!.Invoke(null, new object[] { RAW_SIGNIN_DEEPLINK })!;

            Assert.That(published, Is.True, "TryPublishToBridge must report success when no bridge file is already pending");
            Assert.That(File.Exists(bridgePath), Is.True, "the bridge file must exist at the exact path DeepLinkSentinel's poller reads");
            Assert.That(File.Exists(tempPath), Is.False, "the temp file used for the write-then-rename must not be left behind (atomicity)");

            string writtenContent = File.ReadAllText(bridgePath);

            // This is the SAME parse call DeepLinkSentinel.StartListenForDeepLinksAsync makes on every 200ms
            // check-in: DeepLink.FromJson(fileContent). Reusing it here IS the round-trip assertion — if the
            // writer's DTO shape ever drifts from the reader's (field name, casing, nesting), this fails.
            Result<DeepLink> parsed = DeepLink.FromJson(writtenContent);

            Assert.That(parsed.Success, Is.True, $"the sentinel's own parser must accept the published bridge file; error: {parsed.ErrorMessage}");
            Assert.That(parsed.Value.ValueOf(AppArgsFlags.SIGNIN), Is.EqualTo(SIGNIN_ID), "the signin id must survive the write/read round trip unchanged");
            Assert.That(parsed.Value.ValueOf(AppArgsFlags.AUTH_REQUEST_ID), Is.EqualTo(AUTH_REQUEST_ID), "authRequestId must survive the write/read round trip unchanged — it is what lets the awaiting login claim this exact signin");
        }

        [Test]
        public void NotOverwriteAnAlreadyPendingBridgeFile()
        {
            Assert.That(PUBLISH_METHOD, Is.Not.Null,
                $"publish path missing: DeepLinkSentinel.{PUBLISH_METHOD_NAME} does not exist on this tree.");

            // Written by hand (not via the new DeepLink.ToJson) so this arrange step compiles unpatched too: an
            // unconsumed bridge file already on disk (placed by the launcher, or a racing sibling instance) must
            // never be clobbered by a second publisher.
            File.WriteAllText(bridgePath, "{\"deeplink\":\"decentraland://open?signin=already-pending-signin\"}");

            var published = (bool) PUBLISH_METHOD!.Invoke(null, new object[] { RAW_SIGNIN_DEEPLINK })!;

            Assert.That(published, Is.False, "TryPublishToBridge must refuse to overwrite a bridge file that is already pending");

            Result<DeepLink> stillPending = DeepLink.FromJson(File.ReadAllText(bridgePath));
            Assert.That(stillPending.Success, Is.True);
            Assert.That(stillPending.Value.ValueOf(AppArgsFlags.SIGNIN), Is.EqualTo("already-pending-signin"), "the pre-existing pending signin must be left untouched");
        }
    }
}
