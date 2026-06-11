#if AV_PRO_PRESENT
using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers <see cref="PlayerResponse.Parse"/> — the bit that turns YouTube's
    ///     InnerTube JSON envelope into the data our resolver consumes.
    ///
    ///     The JSON blobs below are hand-shaped to mirror the fields our parser reads
    ///     (videoDetails.isLive / lengthSeconds / isLiveContent, streamingData.formats,
    ///     streamingData.adaptiveFormats, streamingData.hlsManifestUrl, playabilityStatus).
    ///     They deliberately omit all the noise YouTube actually returns — the parser is
    ///     expected to ignore anything it doesn't look up.
    /// </summary>
    public class PlayerResponseShould
    {
        // -------------------------------------------------------------------------
        // playabilityStatus
        // -------------------------------------------------------------------------

        [Test]
        public void Throw_WhenPlayabilityStatusIsError()
        {
            const string json = @"{
                ""playabilityStatus"": { ""status"": ""ERROR"", ""reason"": ""Video unavailable"" }
            }";

            Assert.That(() => PlayerResponse.Parse(json), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Throw_WhenPlayabilityStatusIsLoginRequired()
        {
            const string json = @"{
                ""playabilityStatus"": { ""status"": ""LOGIN_REQUIRED"" }
            }";

            Assert.That(() => PlayerResponse.Parse(json), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void NotThrow_WhenPlayabilityStatusIsOk()
        {
            const string json = @"{
                ""playabilityStatus"": { ""status"": ""OK"" },
                ""videoDetails"": { ""isLive"": false, ""lengthSeconds"": ""212"" },
                ""streamingData"": { }
            }";

            Assert.That(() => PlayerResponse.Parse(json), Throws.Nothing);
        }

        [Test]
        public void NotThrow_WhenPlayabilityStatusIsMissing()
        {
            // Some responses omit playabilityStatus entirely — treat as OK.
            const string json = @"{
                ""videoDetails"": { ""isLive"": false, ""lengthSeconds"": ""100"" },
                ""streamingData"": { }
            }";

            Assert.That(() => PlayerResponse.Parse(json), Throws.Nothing);
        }

        // -------------------------------------------------------------------------
        // Live detection
        // -------------------------------------------------------------------------

        [Test]
        public void DetectLive_WhenIsLiveIsTrue()
        {
            const string json = @"{
                ""videoDetails"": { ""isLive"": true, ""lengthSeconds"": ""0"" },
                ""streamingData"": { ""hlsManifestUrl"": ""https://manifest.googlevideo.com/hls/x.m3u8"" }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.IsLive, Is.True);
        }

        [Test]
        public void DetectLive_WhenLengthIsZero()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""0"" },
                ""streamingData"": { }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.IsLive, Is.True);
        }

        [Test]
        public void DetectNotLive_WhenOnlyHlsManifestPresent()
        {
            // HLS manifest presence alone does NOT imply live — many VODs also expose HLS.
            // Only videoDetails.isLive / isLiveContent / lengthSeconds=="0" are live signals.
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""100"" },
                ""streamingData"": { ""hlsManifestUrl"": ""https://manifest.googlevideo.com/hls/x.m3u8"" }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.IsLive, Is.False);
            Assert.That(response.HlsManifestUrl, Is.EqualTo("https://manifest.googlevideo.com/hls/x.m3u8"));
        }

        [Test]
        public void NotLive_ForOrdinaryVod()
        {
            const string json = @"{
                ""videoDetails"": { ""isLive"": false, ""lengthSeconds"": ""212"" },
                ""streamingData"": { }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.IsLive, Is.False);
            Assert.That(response.HlsManifestUrl, Is.Null.Or.Empty);
        }

        // -------------------------------------------------------------------------
        // Stream extraction
        // -------------------------------------------------------------------------

        [Test]
        public void ExtractMuxedStreams_FromFormatsArray()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""formats"": [
                        {
                            ""itag"": 22,
                            ""url"": ""https://rr1.googlevideo.com/muxed.mp4"",
                            ""mimeType"": ""video/mp4; codecs=\""avc1.64001F, mp4a.40.2\"""",
                            ""bitrate"": 568393,
                            ""width"": 1280,
                            ""height"": 720
                        }
                    ]
                }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.MuxedStreams, Has.Count.EqualTo(1));
            Assert.That(response.VideoOnlyStreams, Is.Empty);

            IStreamInfo stream = response.MuxedStreams[0];
            Assert.That(stream, Is.InstanceOf<MuxedStreamInfo>());
            Assert.That(stream.Url, Is.EqualTo("https://rr1.googlevideo.com/muxed.mp4"));
            Assert.That(stream.Container, Is.EqualTo(Container.Mp4));
            Assert.That(stream.Bitrate.BitsPerSecond, Is.EqualTo(568393));
            Assert.That(((IVideoStreamInfo)stream).VideoResolution.Width, Is.EqualTo(1280));
            Assert.That(((IVideoStreamInfo)stream).VideoResolution.Height, Is.EqualTo(720));
        }

        [Test]
        public void ExtractVideoOnlyStreams_FromAdaptiveFormatsArray()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""adaptiveFormats"": [
                        {
                            ""itag"": 137,
                            ""url"": ""https://rr1.googlevideo.com/video.mp4"",
                            ""mimeType"": ""video/mp4; codecs=\""avc1.640028\"""",
                            ""bitrate"": 4619580,
                            ""width"": 1920,
                            ""height"": 1080
                        }
                    ]
                }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.MuxedStreams, Is.Empty);
            Assert.That(response.VideoOnlyStreams, Has.Count.EqualTo(1));

            IStreamInfo stream = response.VideoOnlyStreams[0];
            Assert.That(stream, Is.InstanceOf<VideoOnlyStreamInfo>());
            Assert.That(stream.Container, Is.EqualTo(Container.Mp4));
            Assert.That(((IVideoStreamInfo)stream).VideoResolution.Height, Is.EqualTo(1080));
        }

        [Test]
        public void SkipAudioOnlyEntries_InAdaptiveFormats()
        {
            // Audio-only adaptive formats lack width/height — our parser drops them because
            // callers only need video streams.
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""adaptiveFormats"": [
                        {
                            ""itag"": 140,
                            ""url"": ""https://rr1.googlevideo.com/audio.mp4"",
                            ""mimeType"": ""audio/mp4; codecs=\""mp4a.40.2\"""",
                            ""bitrate"": 130000
                        }
                    ]
                }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.VideoOnlyStreams, Is.Empty);
        }

        [Test]
        public void SkipEntries_WithoutDirectUrl()
        {
            // Entries that only expose signatureCipher (no plaintext url) can't be used —
            // we don't implement the decipher.
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""formats"": [
                        {
                            ""itag"": 22,
                            ""signatureCipher"": ""s=abc&sp=sig&url=https%3A%2F%2Fexample"",
                            ""mimeType"": ""video/mp4"",
                            ""bitrate"": 500000,
                            ""width"": 1280,
                            ""height"": 720
                        }
                    ]
                }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.MuxedStreams, Is.Empty);
        }

        [Test]
        public void HandleMissingStreamingData_Gracefully()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.MuxedStreams, Is.Empty);
            Assert.That(response.VideoOnlyStreams, Is.Empty);
            Assert.That(response.HlsManifestUrl, Is.Null.Or.Empty);
        }

        // -------------------------------------------------------------------------
        // HasUsableContent — drives the InnerTubeClient fallback chain
        // -------------------------------------------------------------------------

        [Test]
        public void HasUsableContent_True_WhenMuxedStreamPresent()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""formats"": [
                        { ""url"": ""https://a/x.mp4"", ""mimeType"": ""video/mp4"", ""bitrate"": 500000, ""width"": 1280, ""height"": 720 }
                    ]
                }
            }";

            Assert.That(PlayerResponse.Parse(json).HasUsableContent, Is.True);
        }

        [Test]
        public void HasUsableContent_True_WhenHlsManifestPresent()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""0"" },
                ""streamingData"": { ""hlsManifestUrl"": ""https://manifest.googlevideo.com/hls/x.m3u8"" }
            }";

            Assert.That(PlayerResponse.Parse(json).HasUsableContent, Is.True);
        }

        [Test]
        public void HasUsableContent_False_WhenNoStreamsAndNoHls()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": { }
            }";

            Assert.That(PlayerResponse.Parse(json).HasUsableContent, Is.False);
        }

        [Test]
        public void HasUsableContent_False_WhenOnlyCipheredFormats()
        {
            // Response with only signatureCipher entries is effectively empty for us — the
            // fallback chain should move to the next client config.
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""formats"": [
                        {
                            ""signatureCipher"": ""s=abc&url=https%3A%2F%2Fexample"",
                            ""mimeType"": ""video/mp4"",
                            ""bitrate"": 500000, ""width"": 1280, ""height"": 720
                        }
                    ]
                }
            }";

            Assert.That(PlayerResponse.Parse(json).HasUsableContent, Is.False);
        }

        [Test]
        public void HandleMultipleMuxedStreams()
        {
            const string json = @"{
                ""videoDetails"": { ""lengthSeconds"": ""212"" },
                ""streamingData"": {
                    ""formats"": [
                        { ""url"": ""https://a/720.mp4"", ""mimeType"": ""video/mp4"", ""bitrate"": 500000, ""width"": 1280, ""height"": 720 },
                        { ""url"": ""https://a/360.mp4"", ""mimeType"": ""video/mp4"", ""bitrate"": 250000, ""width"": 640,  ""height"": 360 }
                    ]
                }
            }";

            PlayerResponse response = PlayerResponse.Parse(json);

            Assert.That(response.MuxedStreams, Has.Count.EqualTo(2));
        }
    }
}
#endif
