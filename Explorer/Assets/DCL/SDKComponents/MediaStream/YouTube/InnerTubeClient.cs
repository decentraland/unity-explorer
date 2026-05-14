using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Talks directly to YouTube's internal <c>youtubei/v1/player</c> endpoint.
    ///
    ///     Strategy mirrors YoutubeExplode: ANDROID_VR primary (PoT-free, cipher-free) and
    ///     TVHTML5_SIMPLY_EMBEDDED_PLAYER fallback for age-restricted content, plus MWEB and
    ///     WEB_EMBEDDED_PLAYER intermediates as additional safety nets.
    ///
    ///     SELECTION RULE: prefer the FIRST client that returns an HLS or DASH manifest URL,
    ///     not just the first client that returns "any" content. Why: ANDROID_VR often returns
    ///     a low-quality muxed MP4 (itag=18) along with adaptive formats, but no manifest;
    ///     itag=18 has known A/V sync problems. MWEB/TVHTML5 reliably return an HLS or DASH
    ///     manifest URL that AVPro plays cleanly. So we keep searching for a manifest and only
    ///     accept the muxed MP4 as a last-resort fallback if no client offers a manifest.
    ///
    ///     YouTube deprecates client versions periodically. Symptom: HTTP 200 with
    ///     <c>playabilityStatus.status == ERROR</c> and reason "YouTube is no longer supported
    ///     in this application or device." Fix: bump <c>clientVersion</c> in <see cref="CONFIGS"/>
    ///     to match yt-dlp's current values — yt-dlp is the most actively maintained reference.
    /// </summary>
    internal sealed class InnerTubeClient
    {
        private const string TAG = nameof(InnerTubeClient);
        private const string ENDPOINT_BASE = "https://www.youtube.com/youtubei/v1/player?prettyPrint=false&key=";

        // Public InnerTube API keys — not secrets, baked into YouTube's client binaries.
        private const string WEB_API_KEY = "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";
        private const string ANDROID_API_KEY = "AIzaSyA8eiZmM1FaDVjRy-df2KTyQ_vz_yYM39w";

        // Cookie consent acknowledgment. YouTube treats requests without this as
        // non-consenting visitors and aggressively applies bot-detection ("Sign in to
        // confirm you're not a bot"). Matches YoutubeExplode's hardcoded SOCS value.
        private const string SOCS_COOKIE = "SOCS=CAISEwgDEgk4MTM4MzYzNTIaAmVuIAEaBgiApPzGBg";

        // Cookies captured from YouTube response Set-Cookie headers (VISITOR_INFO1_LIVE, YSC, etc.)
        // and replayed on subsequent requests so we look like a persistent session instead of a
        // fresh client. Threadsafe via <see cref="cookieLock"/>.
        private static readonly Dictionary<string, string> persistentCookies = new ();
        private static readonly object cookieLock = new ();

        // Visitor data token extracted from the YouTube home page during session warm-up.
        // YouTube embeds it in the InnerTube context.client.visitorData field — requests
        // without it look like unidentified fresh visitors and trip the bot-detection path
        // ("Sign in to confirm you're not a bot").
        private static volatile string? visitorData;

        // Short-TTL cache of player responses, keyed by VideoId. The resolver makes up to 3
        // calls per video (IsLiveStreamAsync, GetStreamManifestAsync, GetStreamingManifestUrlAsync),
        // and our manifest-preferring fallback can hit up to 4 InnerTube clients per call.
        // Without this cache that's up to 12 network round-trips per resolve.
        private const double PLAYER_RESPONSE_CACHE_TTL_SECONDS = 60;
        private static readonly Dictionary<string, (PlayerResponse Response, System.DateTime ExpiresAtUtc)> playerResponseCache = new ();
        private static readonly object playerResponseCacheLock = new ();

        private static readonly UniTaskCompletionSource warmupCompletion = new ();
        private static int warmupState; // 0 = not started, 1 = in flight, 2 = done

        // Matches the "visitorData":"<base64-ish>" field embedded in the YouTube home page.
        private static readonly System.Text.RegularExpressions.Regex VISITOR_DATA_PATTERN =
            new ("\"visitorData\"\\s*:\\s*\"([^\"]+)\"",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // NOTE: client versions below track yt-dlp's current InnerTube config (as of 2026-01-15).
        // YouTube deprecates old client versions periodically and responds with
        // "YouTube is no longer supported in this application or device" — when that happens,
        // bump these numbers to match yt-dlp's current values in
        // https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/youtube/_base.py
        private static readonly InnerTubeClientConfig[] CONFIGS =
        {
            // Primary: ANDROID_VR (Oculus Quest 3). No PoT, no signature cipher.
            // Config matches YoutubeExplode's master/VideoController.cs exactly — same version,
            // same User-Agent, same minimal payload. Extras like racyCheckOk / html5Preference
            // cause YouTube to omit dashManifestUrl from the response for some videos.
            new (
                clientName: "ANDROID_VR",
                clientVersion: "1.60.19",
                clientNameId: "28",
                apiKey: ANDROID_API_KEY,
                userAgent: "com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip",
                deviceMake: "Oculus",
                deviceModel: "Quest 3",
                osName: "Android",
                osVersion: "12L",
                platform: "MOBILE",
                clientScreen: null,
                includeThirdPartyEmbedUrl: false),

            // Fallback 1: MWEB — mobile web client. No PoT, generally un-ciphered.
            new (
                clientName: "MWEB",
                clientVersion: "2.20260115.01.00",
                clientNameId: "2",
                apiKey: ANDROID_API_KEY,
                userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",
                deviceMake: null,
                deviceModel: null,
                osName: null,
                osVersion: null,
                platform: null,
                clientScreen: null,
                includeThirdPartyEmbedUrl: false),

            // Fallback 2: WEB_EMBEDDED_PLAYER — standard web embed. No PoT.
            new (
                clientName: "WEB_EMBEDDED_PLAYER",
                clientVersion: "1.20260115.01.00",
                clientNameId: "56",
                apiKey: ANDROID_API_KEY,
                userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                deviceMake: null,
                deviceModel: null,
                osName: null,
                osVersion: null,
                platform: null,
                clientScreen: "EMBED",
                includeThirdPartyEmbedUrl: true),

            // Fallback 3: TVHTML5_SIMPLY_EMBEDDED_PLAYER — exact YoutubeExplode fallback for
            // age-restricted content. May serve ciphered URLs our parser silently skips —
            // accepted tradeoff for not shipping a JS engine.
            new (
                clientName: "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
                clientVersion: "2.0",
                clientNameId: "85",
                apiKey: ANDROID_API_KEY,
                userAgent: "Mozilla/5.0 (PlayStation; PlayStation 4/12.00) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.4 Safari/605.1.15",
                deviceMake: null,
                deviceModel: null,
                osName: null,
                osVersion: null,
                platform: null,
                clientScreen: "EMBED",
                includeThirdPartyEmbedUrl: true),
        };

        public async UniTask<PlayerResponse> FetchPlayerResponseAsync(VideoId videoId, CancellationToken ct)
        {
            YouTubeTrace.Log($"innertube.fetch START videoId={videoId.Value}");

            // Cache hit short-circuits the entire fallback chain.
            lock (playerResponseCacheLock)
            {
                if (playerResponseCache.TryGetValue(videoId.Value, out var cached)
                    && cached.ExpiresAtUtc > System.DateTime.UtcNow)
                {
                    YouTubeTrace.Log($"innertube.fetch CACHE-HIT videoId={videoId.Value}");
                    return cached.Response;
                }
            }

            YouTubeTrace.Log($"innertube.warmup START warmupState={warmupState}");
            await EnsureSessionWarmedUpAsync(ct);
            YouTubeTrace.Log($"innertube.warmup END warmupState={warmupState}");

            Exception? lastError = null;
            PlayerResponse fallback = default;
            bool hasFallback = false;

            for (int i = 0; i < CONFIGS.Length; i++)
            {
                if (ct.IsCancellationRequested) break;

                InnerTubeClientConfig config = CONFIGS[i];

                YouTubeTrace.Log($"innertube.config[{i}].{config.ClientName} START");
                try
                {
                    PlayerResponse response = await FetchWithConfigAsync(config, videoId, ct);
                    YouTubeTrace.Log($"innertube.config[{i}].{config.ClientName} END hls={!string.IsNullOrEmpty(response.HlsManifestUrl)} dash={!string.IsNullOrEmpty(response.DashManifestUrl)} muxed={response.MuxedStreams.Count}");

                    ReportHub.Log(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] {config.ClientName} response for {videoId.Value}: " +
                        $"hls={!string.IsNullOrEmpty(response.HlsManifestUrl)}, " +
                        $"dash={!string.IsNullOrEmpty(response.DashManifestUrl)}, " +
                        $"muxed={response.MuxedStreams.Count}, videoOnly={response.VideoOnlyStreams.Count}, isLive={response.IsLive}");

                    // Best case: this client returned an HLS or DASH manifest — clean A/V sync via AVPro.
                    if (response.HasStreamingManifest)
                    {
                        YouTubeTrace.Log($"innertube.fetch END manifest={(string.IsNullOrEmpty(response.HlsManifestUrl) ? "dash" : "hls")} via={config.ClientName} videoId={videoId.Value}");
                        CachePlayerResponse(videoId.Value, response);
                        return response;
                    }

                    // Live videos always need a manifest (HLS) — there's no muxed live path,
                    // so a "muxed-only" response for live is useless and we keep searching.
                    if (response.IsLive)
                    {
                        ReportHub.Log(ReportCategory.MEDIA_STREAM,
                            $"[{TAG}] {config.ClientName} returned no live HLS for {videoId.Value}, trying next client...");
                        continue;
                    }

                    // Worst-case fallback: keep the first usable muxed-only response, but
                    // KEEP TRYING other clients in case one of them has HLS/DASH.
                    if (!hasFallback && response.HasUsableContent)
                    {
                        fallback = response;
                        hasFallback = true;
                    }

                    ReportHub.Log(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] {config.ClientName} had no manifest for {videoId.Value}, trying next client for better quality...");
                }
                catch (OperationCanceledException)
                {
                    YouTubeTrace.Log($"innertube.config[{i}].{config.ClientName} CANCELLED");
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    YouTubeTrace.Log($"innertube.config[{i}].{config.ClientName} ERROR msg={ex.Message}");
                    ReportHub.Log(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] {config.ClientName} failed for {videoId.Value} ({ex.Message}), trying next client...");
                }
            }

            // No manifest from any client — use the muxed-only fallback if we collected one.
            if (hasFallback)
            {
                YouTubeTrace.Log($"innertube.fetch END fallback=muxed videoId={videoId.Value}");
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                    $"[{TAG}] No client returned an HLS/DASH manifest for {videoId.Value}, falling back to muxed MP4 (may have A/V sync issues)");
                CachePlayerResponse(videoId.Value, fallback);
                return fallback;
            }

            YouTubeTrace.Log($"innertube.fetch END all-failed videoId={videoId.Value}");
            throw lastError ?? new InvalidOperationException($"All InnerTube clients failed for {videoId.Value}");
        }

        private static void CachePlayerResponse(string videoId, PlayerResponse response)
        {
            lock (playerResponseCacheLock)
            {
                // Bound the cache size — same approach as YouTubeUrlResolver's url cache.
                if (playerResponseCache.Count >= 50)
                    playerResponseCache.Clear();

                playerResponseCache[videoId] = (response, System.DateTime.UtcNow.AddSeconds(PLAYER_RESPONSE_CACHE_TTL_SECONDS));
            }
        }

        private static async UniTask<PlayerResponse> FetchWithConfigAsync(InnerTubeClientConfig config, VideoId videoId, CancellationToken ct)
        {
            string payload = BuildPayload(config, videoId.Value);
            byte[] body = Encoding.UTF8.GetBytes(payload);

            using var request = new UnityWebRequest(ENDPOINT_BASE + config.ApiKey, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("User-Agent", config.UserAgent);
            request.SetRequestHeader("X-YouTube-Client-Name", config.ClientNameId);
            request.SetRequestHeader("X-YouTube-Client-Version", config.ClientVersion);
            request.SetRequestHeader("Origin", "https://www.youtube.com");
            request.SetRequestHeader("Referer", "https://www.youtube.com/");
            request.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            request.SetRequestHeader("Cookie", BuildCookieHeader());

            if (!string.IsNullOrEmpty(visitorData))
                request.SetRequestHeader("X-Goog-Visitor-Id", visitorData);

            try { await request.SendWebRequest().WithCancellation(ct); }
            catch (UnityWebRequestException ex)
            {
                throw new InvalidOperationException(
                    $"InnerTube request failed ({config.ClientName}) for {videoId.Value}: HTTP {(int)ex.ResponseCode} {ex.Message}", ex);
            }

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(
                    $"InnerTube request failed ({config.ClientName}) for {videoId.Value}: {request.result} — {request.error}");

            CaptureResponseCookies(request);

            string responseText = request.downloadHandler.text;

            if (string.IsNullOrEmpty(responseText))
                throw new InvalidOperationException($"InnerTube returned empty body ({config.ClientName}) for {videoId.Value}");

            return PlayerResponse.Parse(responseText);
        }

        /// <summary>
        ///     Performs a one-time GET against <c>https://www.youtube.com/</c> on first use, to
        ///     accumulate the cookies (VISITOR_INFO1_LIVE, YSC, PREF, ...) and the visitorData
        ///     token that real browsers carry into player calls. Without this preflight, every
        ///     player request looks like a brand-new visitor and YouTube's bot-detection kicks
        ///     in. Concurrent first-callers all await the same warm-up task.
        /// </summary>
        private static async UniTask EnsureSessionWarmedUpAsync(CancellationToken ct)
        {
            // Fast path — already warmed up.
            if (warmupState == 2) return;

            // First caller starts the warm-up; everyone else awaits the shared TCS.
            if (DCLInterlocked.CompareExchange(ref warmupState, 1, 0) == 0)
            {
                bool cancelled = false;

                try
                {
                    await PerformWarmupAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // Reset so the next caller can retry — do NOT mark warmupCompletion
                    // as done, and skip the finally's state advancement.
                    cancelled = true;
                    DCLInterlocked.Exchange(ref warmupState, 0);
                    throw;
                }
                catch (Exception ex)
                {
                    // Best-effort: log and continue. Player calls will proceed without
                    // warm-up cookies; some will still succeed (live streams, public videos).
                    ReportHub.LogError(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] Session warm-up failed (continuing without): {ex.Message}");
                }
                finally
                {
                    if (!cancelled)
                    {
                        warmupState = 2;
                        warmupCompletion.TrySetResult();
                    }
                }
            }
            else
            {
                // Another caller is doing the warm-up — wait for it.
                await warmupCompletion.Task.AttachExternalCancellation(ct);
            }
        }

        private static async UniTask PerformWarmupAsync(CancellationToken ct)
        {
            // Same endpoint and User-Agent YoutubeExplode uses (VideoController.ResolveVisitorDataAsync).
            // The Android UA matters here — switching to a desktop UA makes YouTube return a
            // home-page-shaped response instead of the structured array we expect.
            const string warmupUrl = "https://www.youtube.com/sw.js_data";
            const string warmupUserAgent = "com.google.android.youtube/20.10.38 (Linux; U; ANDROID 11) gzip";

            using var request = UnityWebRequest.Get(warmupUrl);
            request.SetRequestHeader("User-Agent", warmupUserAgent);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            request.SetRequestHeader("Cookie", BuildCookieHeader());

            await request.SendWebRequest().WithCancellation(ct);

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Warm-up GET failed: {request.result} — {request.error}");

            CaptureResponseCookies(request);

            // /sw.js_data returns ")]}'" + a JSON array. Strip the prefix, parse, and pull
            // visitorData from the exact path YoutubeExplode uses: json[0][2][0][0][13].
            // Same magic offsets as their VideoController.cs.
            string? body = request.downloadHandler?.text;

            if (!string.IsNullOrEmpty(body))
            {
                if (body!.StartsWith(")]}'"))
                    body = body.Substring(4);

                try
                {
                    JToken parsed = JToken.Parse(body);
                    string? extracted = (string?)parsed?[0]?[2]?[0]?[0]?[13];

                    if (!string.IsNullOrEmpty(extracted))
                        visitorData = extracted;
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                        $"[{TAG}] /sw.js_data parse failed ({ex.Message}), falling back to home page");
                }
            }

            // Fallback: if /sw.js_data didn't yield visitorData, hit the home page and try there.
            if (string.IsNullOrEmpty(visitorData))
                await ExtractVisitorDataFromHomePageAsync(ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM,
                $"[{TAG}] Session warmed up (cookies={persistentCookies.Count}, visitorData={(visitorData == null ? "no" : "yes")})");
        }

        private static async UniTask ExtractVisitorDataFromHomePageAsync(CancellationToken ct)
        {
            using var request = UnityWebRequest.Get("https://www.youtube.com/");
            request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            request.SetRequestHeader("Cookie", BuildCookieHeader());

            try { await request.SendWebRequest().WithCancellation(ct); }
            catch { return; }

            if (request.result != UnityWebRequest.Result.Success) return;

            CaptureResponseCookies(request);

            string? html = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(html)) return;

            // Home page embeds visitorData as a JSON property — distinct from /sw.js_data's array form.
            System.Text.RegularExpressions.Match match = VISITOR_DATA_PATTERN.Match(html);
            if (match.Success) visitorData = match.Groups[1].Value;
        }

        private static string BuildCookieHeader()
        {
            lock (cookieLock)
            {
                var sb = new StringBuilder(SOCS_COOKIE.Length + persistentCookies.Count * 64);
                sb.Append(SOCS_COOKIE);

                foreach (KeyValuePair<string, string> kvp in persistentCookies)
                    sb.Append("; ").Append(kvp.Key).Append('=').Append(kvp.Value);

                return sb.ToString();
            }
        }

        private static void CaptureResponseCookies(UnityWebRequest request)
        {
            // UnityWebRequest concatenates multiple Set-Cookie headers with ", " when present.
            // BUT Set-Cookie dates also contain commas ("Thu, 01 Jan 2026 ...") — a naive
            // Split(',') would break on those. We split on ", " (comma+space) and then verify
            // each segment starts with a valid "name=value" pattern; segments that don't
            // (like the tail of a date) are skipped.
            string? setCookie = request.GetResponseHeader("Set-Cookie");
            if (string.IsNullOrEmpty(setCookie)) return;

            lock (cookieLock)
            {
                foreach (string entry in setCookie!.Split(new[] { ", " }, StringSplitOptions.None))
                {
                    int semi = entry.IndexOf(';');
                    ReadOnlySpan<char> head = (semi >= 0 ? entry.AsSpan(0, semi) : entry.AsSpan()).Trim();

                    int eq = head.IndexOf('=');
                    if (eq <= 0) continue;

                    // Cookie names are tokens (no spaces, no digits-only). Reject fragments
                    // from date splits like "01 Jan 2026 00:00:00 GMT" which have no '=' or
                    // start with a digit.
                    string name = head[..eq].ToString();
                    if (name.Length == 0 || char.IsDigit(name[0])) continue;

                    string value = head[(eq + 1)..].ToString();

                    // Skip SOCS — we have our own hardcoded value above.
                    if (string.Equals(name, "SOCS", StringComparison.OrdinalIgnoreCase)) continue;

                    persistentCookies[name] = value;
                }
            }
        }

        private static string BuildPayload(InnerTubeClientConfig config, string videoId)
        {
            // Body shape mirrors YoutubeExplode's VideoController.cs exactly. Order of fields,
            // selection of fields, and absence of fields all matter to YouTube — extras like
            // racyCheckOk / html5Preference / timeZone cause the response to omit
            // dashManifestUrl on some videos. JSON-escaping is unnecessary since VideoId.TryParse
            // already constrained the id to [A-Za-z0-9_-]{11}.
            var sb = new StringBuilder(640);
            sb.Append("{\"videoId\":\"").Append(videoId).Append("\",");
            sb.Append("\"contentCheckOk\":true,");
            sb.Append("\"context\":{\"client\":{");
            sb.Append("\"clientName\":\"").Append(config.ClientName).Append("\",");
            sb.Append("\"clientVersion\":\"").Append(config.ClientVersion).Append("\"");

            if (!string.IsNullOrEmpty(config.DeviceMake))
                sb.Append(",\"deviceMake\":\"").Append(config.DeviceMake).Append("\"");

            if (!string.IsNullOrEmpty(config.DeviceModel))
                sb.Append(",\"deviceModel\":\"").Append(config.DeviceModel).Append("\"");

            if (!string.IsNullOrEmpty(config.OsName))
                sb.Append(",\"osName\":\"").Append(config.OsName).Append("\"");

            if (!string.IsNullOrEmpty(config.OsVersion))
                sb.Append(",\"osVersion\":\"").Append(config.OsVersion).Append("\"");

            if (!string.IsNullOrEmpty(config.Platform))
                sb.Append(",\"platform\":\"").Append(config.Platform).Append("\"");

            if (!string.IsNullOrEmpty(config.ClientScreen))
                sb.Append(",\"clientScreen\":\"").Append(config.ClientScreen).Append("\"");

            // visitorData ties our requests to the session cookies captured during warm-up.
            // YouTube treats visitors without a matching visitorData / VISITOR_INFO1_LIVE pair
            // as suspicious and triggers the bot-detection path.
            if (!string.IsNullOrEmpty(visitorData))
                sb.Append(",\"visitorData\":\"").Append(visitorData).Append("\"");

            sb.Append(",\"hl\":\"en\",\"gl\":\"US\",\"utcOffsetMinutes\":0");
            sb.Append("}");

            if (config.IncludeThirdPartyEmbedUrl)
                sb.Append(",\"thirdParty\":{\"embedUrl\":\"https://www.youtube.com\"}");

            sb.Append("}}");
            return sb.ToString();
        }
    }

    internal readonly struct InnerTubeClientConfig
    {
        public string ClientName { get; }
        public string ClientVersion { get; }
        public string ClientNameId { get; }
        public string ApiKey { get; }
        public string UserAgent { get; }
        public string? DeviceMake { get; }
        public string? DeviceModel { get; }
        public string? OsName { get; }
        public string? OsVersion { get; }
        public string? Platform { get; }
        public string? ClientScreen { get; }
        public bool IncludeThirdPartyEmbedUrl { get; }

        public InnerTubeClientConfig(
            string clientName, string clientVersion, string clientNameId, string apiKey, string userAgent,
            string? deviceMake, string? deviceModel, string? osName, string? osVersion,
            string? platform, string? clientScreen, bool includeThirdPartyEmbedUrl)
        {
            ClientName = clientName;
            ClientVersion = clientVersion;
            ClientNameId = clientNameId;
            ApiKey = apiKey;
            UserAgent = userAgent;
            DeviceMake = deviceMake;
            DeviceModel = deviceModel;
            OsName = osName;
            OsVersion = osVersion;
            Platform = platform;
            ClientScreen = clientScreen;
            IncludeThirdPartyEmbedUrl = includeThirdPartyEmbedUrl;
        }
    }

    /// <summary>
    ///     Rich data extracted from a single adaptive-format entry. Carries everything required
    ///     to synthesize an HLS playlist when YouTube doesn't provide one — see <see cref="HlsManifestBuilder"/>.
    /// </summary>
    internal readonly struct AdaptiveFormatData
    {
        public int Itag { get; }
        public string Url { get; }
        public string MimeType { get; }      // e.g. "video/mp4; codecs=\"avc1.640028\""
        public long Bitrate { get; }
        public int? Width { get; }            // null for audio-only
        public int? Height { get; }           // null for audio-only
        public int? Fps { get; }              // null for audio-only
        public int? AudioSampleRate { get; }  // null for video-only
        public int? AudioChannels { get; }    // null for video-only
        public long InitRangeStart { get; }
        public long InitRangeEnd { get; }
        public long IndexRangeStart { get; }
        public long IndexRangeEnd { get; }
        public long ContentLength { get; }

        public bool IsVideo => Width != null && Height != null;
        public bool IsAudio => AudioSampleRate != null;
        public bool HasByteRanges => InitRangeEnd > 0 && IndexRangeEnd > 0;

        public AdaptiveFormatData(int itag, string url, string mimeType, long bitrate,
            int? width, int? height, int? fps, int? audioSampleRate, int? audioChannels,
            long initRangeStart, long initRangeEnd, long indexRangeStart, long indexRangeEnd, long contentLength)
        {
            Itag = itag;
            Url = url;
            MimeType = mimeType;
            Bitrate = bitrate;
            Width = width;
            Height = height;
            Fps = fps;
            AudioSampleRate = audioSampleRate;
            AudioChannels = audioChannels;
            InitRangeStart = initRangeStart;
            InitRangeEnd = initRangeEnd;
            IndexRangeStart = indexRangeStart;
            IndexRangeEnd = indexRangeEnd;
            ContentLength = contentLength;
        }
    }

    /// <summary>
    ///     Thin wrapper over the fields of the InnerTube player JSON response that we actually read.
    /// </summary>
    internal readonly struct PlayerResponse
    {
        public bool IsLive { get; }
        public string? HlsManifestUrl { get; }
        public string? DashManifestUrl { get; }
        public IReadOnlyList<IStreamInfo> MuxedStreams { get; }
        public IReadOnlyList<IStreamInfo> VideoOnlyStreams { get; }

        /// <summary>
        ///     All adaptive-format entries (video AND audio) with the rich metadata required for
        ///     local DASH manifest synthesis. Populated even when YouTube doesn't return a
        ///     dashManifestUrl — we use it to build one ourselves for the muxed-fallback path.
        /// </summary>
        public IReadOnlyList<AdaptiveFormatData> AdaptiveFormats { get; }

        /// <summary>Video duration in seconds — needed for DASH MPD <c>mediaPresentationDuration</c>.</summary>
        public int DurationSeconds { get; }

        /// <summary>
        ///     True if the response carries any playable content at all (manifest, muxed, or
        ///     video-only). Used as the last-ditch acceptance criterion.
        /// </summary>
        public bool HasUsableContent =>
            HasStreamingManifest
            || MuxedStreams.Count > 0
            || VideoOnlyStreams.Count > 0;

        /// <summary>
        ///     True if the response carries an HLS or DASH manifest URL — the formats AVPro
        ///     plays without A/V sync issues. Preferred over muxed MP4 (itag=18 has known
        ///     timing problems). Drives the fallback chain in <see cref="InnerTubeClient"/>.
        /// </summary>
        public bool HasStreamingManifest =>
            !string.IsNullOrEmpty(HlsManifestUrl) || !string.IsNullOrEmpty(DashManifestUrl);

        private PlayerResponse(bool isLive, string? hlsManifestUrl, string? dashManifestUrl,
            IReadOnlyList<IStreamInfo> muxedStreams, IReadOnlyList<IStreamInfo> videoOnlyStreams,
            IReadOnlyList<AdaptiveFormatData> adaptiveFormats, int durationSeconds)
        {
            IsLive = isLive;
            HlsManifestUrl = hlsManifestUrl;
            DashManifestUrl = dashManifestUrl;
            MuxedStreams = muxedStreams;
            VideoOnlyStreams = videoOnlyStreams;
            AdaptiveFormats = adaptiveFormats;
            DurationSeconds = durationSeconds;
        }

        public static PlayerResponse Parse(string json)
        {
            JObject root = JObject.Parse(json);

            // playabilityStatus: OK / ERROR / UNPLAYABLE / LOGIN_REQUIRED / AGE_VERIFICATION_REQUIRED
            string? status = (string?)root["playabilityStatus"]?["status"];

            if (!string.IsNullOrEmpty(status) && !string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                string? reason = (string?)root["playabilityStatus"]?["reason"];
                throw new InvalidOperationException($"YouTube refused playback: {status}{(string.IsNullOrEmpty(reason) ? string.Empty : $" — {reason}")}");
            }

            JToken? details = root["videoDetails"];
            JToken? streamingData = root["streamingData"];

            string? hlsManifestUrl = (string?)streamingData?["hlsManifestUrl"];
            string? dashManifestUrl = (string?)streamingData?["dashManifestUrl"];

            // NOTE: hlsManifestUrl/dashManifestUrl are NOT live signals — VODs also expose them
            // (we prefer them over muxed MP4 for clean A/V sync). Only videoDetails flags +
            // lengthSeconds==0 actually indicate a live broadcast.
            bool isLive = (bool?)details?["isLive"] == true
                          || (bool?)details?["isLiveContent"] == true
                          || (string?)details?["lengthSeconds"] == "0";

            int durationSeconds = 0;
            string? lengthSecondsStr = (string?)details?["lengthSeconds"];
            if (!string.IsNullOrEmpty(lengthSecondsStr) && int.TryParse(lengthSecondsStr, out int parsedDuration))
                durationSeconds = parsedDuration;

            var muxed = new List<IStreamInfo>();
            var videoOnly = new List<IStreamInfo>();
            var adaptiveFormatsList = new List<AdaptiveFormatData>();

            if (streamingData?["formats"] is JArray formats)
                ParseMuxedFormats(formats, muxed);

            if (streamingData?["adaptiveFormats"] is JArray adaptive)
                ParseAdaptiveFormats(adaptive, videoOnly, adaptiveFormatsList);

            return new PlayerResponse(isLive, hlsManifestUrl, dashManifestUrl, muxed, videoOnly, adaptiveFormatsList, durationSeconds);
        }

        private static void ParseMuxedFormats(JArray array, List<IStreamInfo> muxedTarget)
        {
            foreach (JToken entry in array)
            {
                string? url = (string?)entry["url"];
                if (string.IsNullOrEmpty(url)) continue;

                int? width = (int?)entry["width"];
                int? height = (int?)entry["height"];
                if (width == null || height == null) continue;

                Container container = ContainerExtensions.ParseMimeType((string?)entry["mimeType"]);
                var bitrate = new Bitrate((long?)entry["bitrate"] ?? 0);
                var resolution = new VideoResolution(width.Value, height.Value);

                muxedTarget.Add(new MuxedStreamInfo(container, bitrate, url!, resolution));
            }
        }

        private static void ParseAdaptiveFormats(JArray array, List<IStreamInfo> videoOnlyTarget, List<AdaptiveFormatData> adaptiveTarget)
        {
            foreach (JToken entry in array)
            {
                string? url = (string?)entry["url"];

                // Skip entries that only expose signatureCipher — we don't implement the decipher.
                if (string.IsNullOrEmpty(url)) continue;

                int itag = (int?)entry["itag"] ?? 0;
                string mimeType = (string?)entry["mimeType"] ?? string.Empty;
                long bitrateValue = (long?)entry["bitrate"] ?? 0;
                int? width = (int?)entry["width"];
                int? height = (int?)entry["height"];
                int? fps = (int?)entry["fps"];

                int? audioSampleRate = null;
                string? audioSampleRateStr = (string?)entry["audioSampleRate"];
                if (!string.IsNullOrEmpty(audioSampleRateStr) && int.TryParse(audioSampleRateStr, out int parsedAsr))
                    audioSampleRate = parsedAsr;

                int? audioChannels = (int?)entry["audioChannels"];

                long initStart = ParseRangeEndpoint(entry["initRange"]?["start"]);
                long initEnd = ParseRangeEndpoint(entry["initRange"]?["end"]);
                long indexStart = ParseRangeEndpoint(entry["indexRange"]?["start"]);
                long indexEnd = ParseRangeEndpoint(entry["indexRange"]?["end"]);

                long contentLength = 0;
                string? contentLengthStr = (string?)entry["contentLength"];
                if (!string.IsNullOrEmpty(contentLengthStr) && long.TryParse(contentLengthStr, out long parsedClen))
                    contentLength = parsedClen;

                // Capture rich data for DASH synthesis (covers BOTH video and audio entries).
                adaptiveTarget.Add(new AdaptiveFormatData(
                    itag, url!, mimeType, bitrateValue,
                    width, height, fps, audioSampleRate, audioChannels,
                    initStart, initEnd, indexStart, indexEnd, contentLength));

                // Also populate the simple IStreamInfo collection for the muxed-fallback path
                // — but only for video entries (audio-only is useless on its own to AVPro).
                if (width != null && height != null)
                {
                    Container container = ContainerExtensions.ParseMimeType(mimeType);
                    var bitrate = new Bitrate(bitrateValue);
                    var resolution = new VideoResolution(width.Value, height.Value);
                    videoOnlyTarget.Add(new VideoOnlyStreamInfo(container, bitrate, url!, resolution));
                }
            }
        }

        private static long ParseRangeEndpoint(JToken? token)
        {
            if (token == null) return 0;
            string? str = (string?)token;
            if (string.IsNullOrEmpty(str)) return 0;
            return long.TryParse(str, out long parsed) ? parsed : 0;
        }
    }
}
