using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System.IO;
using System.Net;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class LocalHlsPlaylistServerShould
    {
        private static string Get(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            using var response = (HttpWebResponse)request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream()!);
            return reader.ReadToEnd();
        }

        private static HttpStatusCode StatusOf(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                using var response = (HttpWebResponse)request.GetResponse();
                return response.StatusCode;
            }
            catch (WebException e) when (e.Response is HttpWebResponse errorResponse)
            {
                using (errorResponse) { return errorResponse.StatusCode; }
            }
        }

        [Test]
        public void ServeRegisteredPlaylistsOverLoopback()
        {
            var playlists = new HlsManifestBuilder.PlaylistSet("#EXTM3U master", "#EXTM3U video", "#EXTM3U audio");

            string? masterUrl = LocalHlsPlaylistServer.TryRegister("serve-test", playlists);

            Assert.That(masterUrl, Is.Not.Null);
            Assert.That(masterUrl, Does.StartWith("http://127.0.0.1:"));
            Assert.That(Get(masterUrl!), Is.EqualTo("#EXTM3U master"));
            Assert.That(Get(masterUrl!.Replace(HlsManifestBuilder.MASTER_PLAYLIST_NAME, HlsManifestBuilder.VIDEO_PLAYLIST_NAME)), Is.EqualTo("#EXTM3U video"));
            Assert.That(Get(masterUrl.Replace(HlsManifestBuilder.MASTER_PLAYLIST_NAME, HlsManifestBuilder.AUDIO_PLAYLIST_NAME)), Is.EqualTo("#EXTM3U audio"));
        }

        [Test]
        public void ReplaceEntryWhenKeyIsReRegistered()
        {
            var first = new HlsManifestBuilder.PlaylistSet("#EXTM3U v1", "v", "a");
            var second = new HlsManifestBuilder.PlaylistSet("#EXTM3U v2", "v", "a");

            string firstUrl = LocalHlsPlaylistServer.TryRegister("replace-test", first)!;
            string secondUrl = LocalHlsPlaylistServer.TryRegister("replace-test", second)!;

            Assert.That(secondUrl, Is.Not.EqualTo(firstUrl));
            Assert.That(Get(secondUrl), Is.EqualTo("#EXTM3U v2"));
            Assert.That(StatusOf(firstUrl), Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public void RejectUnknownTokensAndPlaylistNames()
        {
            var playlists = new HlsManifestBuilder.PlaylistSet("m", "v", "a");
            string masterUrl = LocalHlsPlaylistServer.TryRegister("reject-test", playlists)!;

            string baseUrl = masterUrl[..(masterUrl.LastIndexOf('/') + 1)];
            string serverRoot = masterUrl[..masterUrl.IndexOf('/', "http://".Length)];

            Assert.That(StatusOf(baseUrl + "other.m3u8"), Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(StatusOf(serverRoot + "/unknown-token/master.m3u8"), Is.EqualTo(HttpStatusCode.NotFound));
        }
    }
}
