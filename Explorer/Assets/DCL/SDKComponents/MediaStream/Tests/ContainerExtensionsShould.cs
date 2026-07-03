#if AV_PRO_PRESENT
using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class ContainerExtensionsShould
    {
        [TestCase("video/mp4", Container.Mp4)]
        [TestCase("video/mp4; codecs=\"avc1.640028, mp4a.40.2\"", Container.Mp4)]
        [TestCase("audio/mp4; codecs=\"mp4a.40.2\"", Container.Mp4)]
        [TestCase("video/webm; codecs=\"vp9\"", Container.WebM)]
        [TestCase("audio/webm", Container.WebM)]
        [TestCase("video/3gpp", Container.Tgpp)]
        [TestCase("video/mov", Container.Mov)]
        [TestCase("video/quicktime", Container.Mov)]
        [TestCase("audio/mpeg", Container.Mp3)]
        [TestCase("audio/mp3", Container.Mp3)]
        public void ParseKnownMimeType(string mimeType, Container expected)
        {
            Assert.That(ContainerExtensions.ParseMimeType(mimeType), Is.EqualTo(expected));
        }

        [TestCase("VIDEO/MP4; codecs=\"x\"", Container.Mp4)]
        [TestCase("Video/WebM", Container.WebM)]
        public void ParseIsCaseInsensitive(string mimeType, Container expected)
        {
            Assert.That(ContainerExtensions.ParseMimeType(mimeType), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-mime-type")]
        [TestCase("video/")]
        [TestCase("video/avi")]
        [TestCase("application/octet-stream")]
        public void ParseUnknownReturnsUnknown(string? mimeType)
        {
            Assert.That(ContainerExtensions.ParseMimeType(mimeType), Is.EqualTo(Container.Unknown));
        }
    }
}
#endif
