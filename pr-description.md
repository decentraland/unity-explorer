# Pull Request Description

## What does this PR change?

Extends the AVPro Video media player to support **YouTube** and **Google Drive** video URLs by resolving them into direct stream URLs before passing to AVPro.

AVPro Video cannot play YouTube (`youtube.com/watch?v=...`) or Google Drive (`drive.google.com/file/d/.../view`) URLs directly because these are webpage URLs, not direct video file URLs. This PR adds a resolver layer that transparently converts these URLs into streamable direct URLs.

### YouTube Support
- Uses [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) (v6.5.7) .NET library for client-side resolution
- **VOD videos**: Resolves to best MP4/H.264 stream at up to 1080p (`googlevideo.com/videoplayback?...`)
- **Live streams**: Resolves to HLS `.m3u8` manifest (AVPro handles HLS natively)
- In-memory cache with 90-minute TTL (YouTube URLs expire after ~2-4 hours)
- Automatic retry on failure, 10-second timeout
- Expired URL re-resolution on playback error
- Swappable `IYouTubeUrlResolver` interface for future replacement

### Google Drive Support
- Simple URL rewrite — no external library needed
- Transforms sharing URLs to `drive.usercontent.google.com/download?id={ID}&export=view`
- Skips HEAD reachability check (causes exceptions in Unity's HTTP stack) and goes directly to GET-based check
- Extracts file ID from `drive.google.com/file/d/{ID}/view` and `drive.google.com/open?id={ID}` formats

### Architecture
Resolution is inserted into `OpenMediaPromise.UrlReachabilityResolveAsync()` — the existing async checkpoint between "URL received" and "media opened". This means **zero changes** to `MediaAddress`, `MultiMediaPlayer`, or the REnum discriminated unions. The resolved direct URL replaces the original in the promise, and the entire downstream flow works unchanged.

```
Scene provides URL (YouTube / Google Drive / direct)
    -> OpenMediaPromise detects URL type
    -> YouTube: YoutubeExplode resolves to direct stream URL
    -> Google Drive: URL rewrite to usercontent endpoint
    -> Direct URL: pass through (existing behavior)
    -> Reachability check on resolved URL
    -> AVPro opens direct URL
```

### Risks & Limitations
- **YoutubeExplode fragility**: YouTube changes internal APIs frequently. The `IYouTubeUrlResolver` interface makes the resolver swappable without touching other code.
- **DRM content**: Cannot be resolved — fails gracefully as `VsError`.
- **Google Drive**: File must be publicly shared ("anyone with the link").

## Test Instructions

### Prerequisites
- [ ] AVPro Video package installed (`AV_PRO_PRESENT` define active)
- [ ] Unity project compiles without errors
- [ ] Network access available (resolver makes HTTP requests)

### Test Steps

**YouTube VOD:**
1. In a scene, set a `VideoPlayer` component's `Src` to `https://www.youtube.com/watch?v=dQw4w9WgXcQ`
2. Enter play mode
3. Check console for `[YouTubeResolver] Resolved VOD` log
4. Video should play after a few seconds of resolution + buffering

**YouTube Live Stream:**
1. Set `Src` to a YouTube live stream URL (e.g. a 24/7 lofi stream)
2. Check console for `[YouTubeResolver] Detected live stream` and `Resolved live stream to HLS manifest`
3. Live stream should play via HLS

**Google Drive:**
1. Set `Src` to `https://drive.google.com/file/d/1RnLGZwGDLbB6iLPcr6kW8AshzsnuNDTk/view?resourcekey`
2. Check console for `[GoogleDrive] Resolved to direct URL`
3. Video should play (no HEAD exception in logs)

**Regular URLs (regression):**
1. Set `Src` to a direct MP4/HLS URL (e.g. `https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4`)
2. Verify it still plays normally — no YouTube/Google Drive resolution should trigger

### Additional Testing Notes
- Test with private/unavailable YouTube videos — should fail gracefully with `VsError`
- Test YouTube URL caching: resolve same video twice, second time should log `[YouTubeResolver] Cache hit`
- Test on macOS: HLS interval guard should still apply for live stream `.m3u8` URLs
- Verify no LINQ usage in hot paths (resolution is async, not in `Update()`)

## Quality Checklist
- [x] Changes have been tested locally
- [x] Performance impact has been considered (resolution is async, cache avoids repeat calls, no allocations in Update loop)
- [ ] Documentation has been updated (if required)
- [ ] For SDK features: Test scene is included