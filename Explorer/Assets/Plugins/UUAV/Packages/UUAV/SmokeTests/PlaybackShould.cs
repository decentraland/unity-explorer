using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UUAV.SmokeTests
{
    public class PlaybackShould
    {
        private const string Url =
            "https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/1080/Big_Buck_Bunny_1080_10s_1MB.mp4";

        [UnityTest]
        [Explicit("hits the network; run manually to smoke-test a real decode")]
        public IEnumerator DecodeFramesOverHttps()
        {
            UUAVPlayer player = UUAVPlayer.New();
            Debug.Log("UUAV-SMOKE: opening " + Url);
            player.OpenMedia(Url);

            float deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline
                   && player.State != UUAVState.Ready
                   && player.State != UUAVState.Playing
                   && player.State != UUAVState.Error)
                yield return null;

            Debug.Log("UUAV-SMOKE: state after open = " + player.State);
            Assert.AreNotEqual(UUAVState.Error, player.State, "player reported Error after open");

            if (player.TryGetMediaInfo(out MediaInfo info))
            {
                UUAVPlayer.MediaInfo_M m = UUAVPlayer.MediaInfo_M.From(info);
                Debug.Log($"UUAV-SMOKE: info {m.Width}x{m.Height} dur={m.Duration:F2}s fps={m.Framerate:F2} " +
                          $"vcodec={m.VideoCodec} pixfmt={m.PixelFormat} acodec={m.AudioCodec} hasVideo={m.HasVideo}");
                Assert.Greater(m.Width, 0u, "media width is zero");
                Assert.Greater(m.Height, 0u, "media height is zero");
            }
            else Assert.Fail("TryGetMediaInfo returned false after open");

            player.Play();
            double t0 = player.CurrentTime;
            float until = Time.realtimeSinceStartup + 20f;
            int textureFrames = 0;
            while (Time.realtimeSinceStartup < until)
            {
                if (player.CurrentTexture != null) textureFrames++;
                if (player.CurrentTime > t0 + 1.0) break;
                yield return null;
            }

            RenderTexture tex = player.CurrentTexture;
            Debug.Log($"UUAV-SMOKE: state={player.State} t0={t0:F3} t={player.CurrentTime:F3} " +
                      $"texture={(tex == null ? "NULL" : tex.width + "x" + tex.height)} textureFrames={textureFrames}");

            Assert.IsNotNull(tex, "CurrentTexture never became non-null");
            Assert.Greater(tex.width, 0, "texture width is zero");
            Assert.Greater(player.CurrentTime, t0, "playback clock did not advance");
            Debug.Log("UUAV-SMOKE: PASS");
        }
    }
}
