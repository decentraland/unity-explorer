using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class GoogleDriveMediaExtensionsShould
    {
        [TestCase("https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view")]
        [TestCase("https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view?usp=sharing")]
        [TestCase("https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view?resourcekey")]
        [TestCase("https://drive.google.com/open?id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk")]
        [TestCase("https://drive.google.com/uc?export=download&id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk")]
        public void DetectGoogleDriveUrls(string url)
        {
            Assert.That(url.IsGoogleDriveUrl(), Is.True);
        }

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://example.com/video.mp4")]
        [TestCase("https://www.google.com/search?q=test")]
        [TestCase("https://docs.google.com/document/d/abc123/edit")]
        [TestCase("https://docs.google.com/spreadsheets/d/abc123/edit")]
        [TestCase("livekit-video://current-stream")]
        [TestCase("")]
        public void RejectNonGoogleDriveUrls(string url)
        {
            Assert.That(url.IsGoogleDriveUrl(), Is.False);
        }

        [TestCase("https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view",
            "https://drive.usercontent.google.com/download?id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk&export=view")]
        [TestCase("https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view?usp=sharing",
            "https://drive.usercontent.google.com/download?id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk&export=view")]
        [TestCase("https://drive.google.com/open?id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk",
            "https://drive.usercontent.google.com/download?id=1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk&export=view")]
        public void ResolveDirectUrl(string input, string expected)
        {
            Assert.That(input.ResolveGoogleDriveDirectUrl(), Is.EqualTo(expected));
        }

        [TestCase("https://drive.google.com/drive/folders/abc123")]
        [TestCase("https://drive.google.com/")]
        public void ReturnNullForUrlsWithoutFileId(string url)
        {
            Assert.That(url.ResolveGoogleDriveDirectUrl(), Is.Null);
        }
    }
}
