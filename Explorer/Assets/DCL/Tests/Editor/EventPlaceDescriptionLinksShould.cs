using CommunicationData.URLHelpers;
using DCL.UI;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using TMPro;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     An event description is written by the event's owner — who can edit it after the event was approved — and a
    ///     place description is copied verbatim out of a deployed scene's manifest. Both land on a rich-text label that
    ///     is then linkified, so the label has to keep interpreting the linkifier's own markup while treating the
    ///     description itself as plain words, and a link it does produce has to ask the user before opening (SEC-084).
    /// </summary>
    public class EventPlaceDescriptionLinksShould
    {
        private const string URL = "https://example.com";

        private GameObject canvasRoot = null!;
        private TMP_Text descriptionLabel = null!;
        private IMVCManagerMenusAccessFacade menus = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // ViewDependencies is a process-wide singleton that throws if it is initialized twice and offers no
            // reset, so the fixture claims it once. The menus facade is the only dependency this path reaches; the
            // rest stay unset so a test that starts needing one fails here instead of leaning on a stub.
            menus = Substitute.For<IMVCManagerMenusAccessFacade>();

            ViewDependencies.Initialize(new ViewDependencies(
                eventSystem: null!,
                globalUIViews: menus,
                clipboardManager: null!,
                cursor: null!,
                contextMenuOpener: null!,
                web3IdentityCache: null!,
                confirmationDialogOpener: null!));
        }

        [SetUp]
        public void SetUp()
        {
            // A label under a canvas, the hierarchy the description panels put theirs in.
            canvasRoot = new GameObject(nameof(EventPlaceDescriptionLinksShould), typeof(Canvas));
            descriptionLabel = new GameObject("DescriptionLabel").AddComponent<TextMeshProUGUI>();
            descriptionLabel.transform.SetParent(canvasRoot.transform);

            // The facade is shared across the fixture, so calls a previous test recorded would otherwise count.
            menus.ClearReceivedCalls();
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(canvasRoot);

        [TestCase("<link=\"decentraland://?x\">click here</link>", "click here")]
        [TestCase("<size=400%>enormous</size>", "enormous")]
        [TestCase("<color=#FF0000>alarming</color>", "alarming")]
        public void RenderADescriptionsOwnMarkupAsLiteralText(string description, string words)
        {
            // Act
            descriptionLabel.SetAuthorTextWithClickeableLinks(description);

            // Assert — TMP only starts reading a tag at '<', so the absence of both brackets is what leaves the
            // description unable to open a link of its own, hide the copy around it, or blow up the panel's layout.
            Assert.That(descriptionLabel.text, Does.Not.Contain("<").And.Not.Contain(">"), description);

            // The author's words are still there: escaping neutralizes the markup, it does not drop the text.
            StringAssert.Contains(words, descriptionLabel.text);
        }

        [Test]
        public void StillLinkifyABareUrlInADescription()
        {
            // Act
            descriptionLabel.SetAuthorTextWithClickeableLinks($"Visit {URL} for details");

            // Assert — the linkifier's own markup is emitted and left live, which is what a description carrying an
            // ordinary URL depends on.
            StringAssert.Contains($"<link={URL}>{URL}</link>", descriptionLabel.text);
            Assert.IsNotNull(descriptionLabel.GetComponent<TMP_Text_ClickeableLink>());
        }

        [Test]
        public void OpenALinkifiedUrlOnlyThroughTheConsentPrompt()
        {
            // Arrange
            descriptionLabel.SetAuthorTextWithClickeableLinks($"Visit {URL} for details");

            // Act — the dispatch OnPointerClick performs once it has hit-tested a link.
            descriptionLabel.GetComponent<TMP_Text_ClickeableLink>().ActivateLink(URL);

            // Assert — the URL reaches the prompt the user has to confirm, never the browser sink directly. The prompt
            // is also where the http(s)-only policy is applied, so nothing on this path re-checks the scheme.
            menus.Received(1).ShowExternalUrlPromptAsync(URLAddress.FromString(URL), Arg.Any<CancellationToken>());
        }
    }
}
