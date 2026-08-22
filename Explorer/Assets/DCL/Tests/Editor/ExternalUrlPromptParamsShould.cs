using DCL.ExternalUrlPrompt;
using DCL.Passport.Fields;
using NUnit.Framework;
using System;
using UnityEditor;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    /// Every caller that opens an external URL (scene <c>openExternalUrl</c>, passport profile links, chat
    /// hyperlinks) builds a <see cref="ExternalUrlPromptController.Params"/>, so this is the shared gate that
    /// keeps non-web schemes away from the OS handler (SEC-008).
    /// </summary>
    public class ExternalUrlPromptParamsShould
    {
        private const string PREFAB_PATH = "Assets/DCL/ExternalUrlPrompt/Assets/ExternalUrlPrompt.prefab";
        private const string LINK_FIELD_PREFAB_PATH = "Assets/DCL/Passport/Prefabs/Link_PassportField.prefab";

        [TestCase("smb://attacker-host/share")]          // UNC → NTLM credential leak
        [TestCase("file:///Users/victim/secrets.txt")]   // local file disclosure
        [TestCase("mailto:someone@example.com")]
        [TestCase("decentraland://?realm=evil.example.com")] // launcher re-invocation (SEC-028)
        [TestCase("steam://run/1")]                      // installed-app protocol handler
        [TestCase("javascript:alert(1)")]
        [TestCase("not a url")]
        [TestCase("")]
        public void RejectNonWebScheme(string url)
        {
            // Act
            var parameters = new ExternalUrlPromptController.Params(url);

            // Assert
            Assert.IsNull(parameters.Uri, url);
        }

        [TestCase("https://decentraland.org/whitepaper.pdf")]
        [TestCase("http://example.com")]
        public void AcceptWebScheme(string url)
        {
            // Act
            var parameters = new ExternalUrlPromptController.Params(url);

            // Assert
            Assert.IsNotNull(parameters.Uri, url);
        }

        [Test]
        public void StripMarkupFromTheDisplayedDestination()
        {
            // Arrange — TMP markup smuggled into an otherwise valid https URL, which the prompt would
            // otherwise render as rich text and use to hide the real destination.
            var parameters = new ExternalUrlPromptController.Params(
                "https://evil.example.com/?x=<size=1><color=#00000000>decentraland.org");

            // Act
            Uri? displayed = parameters.Uri;

            // Assert
            Assert.IsNotNull(displayed);
            Assert.AreEqual("evil.example.com", displayed?.Host);
            Assert.That(displayed?.AbsoluteUri, Does.Not.Contain("<").And.Not.Contain(">"));
        }

        [Test]
        public void RenderTheDestinationAsPlainText()
        {
            // Arrange
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH).GetComponent<ExternalUrlPromptView>();

            // Assert — the two labels carry nothing but the attacker-supplied destination, so rich text is off
            // in the prefab. Guarded here because a prefab edit could otherwise re-enable it unnoticed.
            Assert.IsFalse(view.DomainText.richText, nameof(view.DomainText));
            Assert.IsFalse(view.UrlText.richText, nameof(view.UrlText));
        }

        [Test]
        public void RenderAnotherUsersLinkTitleAsPlainText()
        {
            // Arrange
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(LINK_FIELD_PREFAB_PATH).GetComponent<LinkPassportFieldView>();

            // Assert — the title is authored by whoever owns the passport, and it is the label the viewer reads
            // before clicking. Markup there could dress an arbitrary destination up as a familiar one, which is
            // what makes the prompt's single approval easy to obtain (SEC-008).
            Assert.IsFalse(view.Title.richText, nameof(view.Title));
        }
    }
}
