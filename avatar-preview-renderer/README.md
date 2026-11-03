# Avatar Preview Renderer

Part of the [unity-explorer monorepo](../README.md#%EF%B8%8F-repository-layout): every merge to `dev` touching this folder auto-releases a `avatar-preview-renderer/vX.Y.Z` tag, which [wearable-preview](https://github.com/decentraland/wearable-preview) vendors via `npm run update-unity` (see [Build & CI § Avatar Preview Renderer](../docs/build-and-ci.md#avatar-preview-renderer)).

This project is responsible for rendering previews of the user profile and wearables used on the Decentraland Marketplace, Authentication screen, Profile page, Builder, and Configurator.

It builds as a Web target, uses WebGPU as it's rendering backend, and shares the Toon and Scene shaders with the Explorer client so we get the same visual representation as the users will see in the game

## Usage

Primarily the renderer will fetch it's configuration from URL parameters passed to it, but can also be configured dynamically after initial load via a SendMessage call.

The renderer can run in five different modes, depending on its usage: Marketplace, Authentication, Profile, Builder, Configurator.

## Parameters

* `mode`: Which mode we run as. Possible values:
  * `marketplace` (default)
  * `authentication`
  * `profile`
  * `builder`
  * `configurator`
* `profile`: The id (wallet address) of the profile to use or one of the default profiles (`default1` - `default15`)
* `username`: Used only in `configurator` mode. Sets the username to be displayed.
* `emote`: The default emote that the avatar will play. Only used if no emote override is present. Possible values:
  * `idle` (default)
  * `clap`
  * `dab`
  * `dance`
  * `fashion`
  * `fashion-2`
  * `fashion-3`
  * `fashion-4`
  * `love`
  * `money`
  * `fist-pump`
  * `head-explode`
* `urn`: An URN address of a wearable or emote to load. It will override any existing wearable in the same category already present on the profile that has been loaded. Can be included multiple times in `marketplace` and `builder` mode to load multiple wearables.
* `type`: Which view to open in. Only used in `marketplace` mode, and only when a single wearable is being previewed (an emote or several urns at once can only be shown on the avatar). If omitted, the view the user last switched to is used. Possible values:
  * `wearable` - the item on its own
  * `avatar` - the item worn by the avatar
* `disableSwitcher`: Hides the avatar / item switcher, so the view stays as it was requested. Only used in `marketplace` mode. Default is `false`.
* `background`: The background color to use for the renderer. It must be in hex and not include the leading # (e.g. `ff00ff`). It may include alpha for a transparent background. Default is transparent.
* `shadow`: Whether the avatar casts a shadow on the floor. The shadow is drawn into the canvas alpha, so over a transparent background it composites onto whatever sits behind the renderer. Default is `on`; pass `shadow=off` to remove it. Not used in `configurator` mode, and the item-alone view of `marketplace` mode has no shadow either way.
* `glow`: Whether a soft pool of light is drawn on the floor under the avatar, so it reads as standing on a lit surface rather than floating. Brightens the canvas the same way `shadow` darkens it, so over a transparent background it lifts whatever sits behind the renderer. Default is `on`; pass `glow=off` to remove it. Same modes as `shadow`, and unlike `shadow` it also appears in the item-alone view of `marketplace` mode, where it sits under the floating item.
* `skinColor`: The color to use for the skin of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `hairColor`: The color to use for the hair of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `eyeColor`: The color to use for the eyes of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `bodyShape`: The body shape to use. Possible values:
  * `urn:decentraland:off-chain:base-avatars:BaseMale`
  * `urn:decentraland:off-chain:base-avatars:BaseFemale`
* `projection`: The projection to use for the camera. Possible values:
  * `perspective` (default)
  * `orthographic`
* `base64`: A base64 encoded definition of a wearable or emote to load.
* `contract`: The contract address of a wearable or emote to load.
* `item`: The item id of a wearable or emote to load.
* `token`: The token id of a wearable or emote to load.
* `env`: The environment to use for API calls. Possible values:
  * `prod` (default) - uses ORG
  * `dev` - uses ZONE


## Modes

Depending on the mode, not all parameters are used. These are the valid parameters in each mode:

### Marketplace
* `background`
* `shadow` (optional)
* `glow` (optional)
* `profile`
* `urn` or `contract` & `item` or `contract` & `token`
  * Multiple urn parameters may be used to preview several items at once (a cart, an outfit). The remaining categories are still filled from the profile, and the passed urns override by category, so the last urn wins its category. Several items can only be shown worn by the avatar, since the item-alone view renders a single item.
* `emote`
* `type` (optional)
* `disableSwitcher` (optional)

### Profile
* `background`
* `shadow` (optional)
* `glow` (optional)
* `profile`
* `emote`

### Authentication
* `background`
* `shadow` (optional)
* `glow` (optional)
* `profile`
* `emote`

### Builder
* `background`
* `shadow` (optional)
* `glow` (optional)
* `bodyShape`
* `eyeColor`
* `hairColor`
* `skinColor`
* `urn`
  * Multiple urn parameters may be used to load several wearables. The categories of the wearables must be unique (e.g. two urns cannot both be for "upper_body")
* `emote`
* `base64`

### Configurator
* `username`
* `background` (optional)


## Example URLs

These examples show how to construct URLs with the correct parameters (replace the domain with your own deployment):

- **Configurator**:  
  `?mode=configurator&username=Miha`

- **Marketplace**:  
  `?mode=marketplace&profile=default1&urn=urn:decentraland:off-chain:base-avatars:rasta`

- **Profile**:  
  `?mode=profile&profile=default1`

- **Authentication**:  
  `?mode=authentication&profile=default1`
## Dynamic configuration

Most properties can be set dynamically after the renderer is already running by calling `SendMessage('JSBridge', 'MethodName', 'value')` on the `unityInstance` object returned after initialization.

Example usage:

```javascript
unityInstance.SendMessage('JSBridge', 'SetEmote', 'clap');
unityInstance.SendMessage('JSBridge', 'SetSkinColor', 'ff0000');
```

Every call of this function will trigger a reload of the entire avatar.
For a full list of available functions check [JSBridge](Assets/Scripts/JSBridge.cs).

### Special cases

* `SetUrns`
  * The input should be either a single URN or a list of urns separated by commas.
  * Example: `unityInstance.SendMessage('JSBridge', 'SetUrns', 'urn:decentraland:off-chain:base-avatars:kilt,urn:decentraland:off-chain:base-avatars:full_beard,urn:decentraland:off-chain:base-avatars:blue_bandana');`

## Taking screenshots

The renderer has the ability to take a screenshot and provide the png as a base64 encoded string.

To take a screenshot you call:
```javascript
unityInstance.SendMessage('JSBridge', 'TakeScreenshot');
```

This will not return any value. When the screenshot is taken a message is sent to the window parent which can be listened to, to retrieve the screenshot:

```javascript
window.parent.postMessage(
{
  type: 'unity-screenshot',
  data: base64str
}
```

Example:
```javascript
window.addEventListener('message', (event) => {
  if (event.data?.type === 'unity-screenshot') {
    const base64 = event.data.data;
    console.log('Received screenshot:', base64);
  }
});
```

## Render server (native, CPU)

The same project also builds as a native Linux player that writes transparent PNG stills of wearables and emotes, with no browser and no GPU. It renders through Mesa's llvmpipe (OpenGL on the CPU) inside a virtual X display. The server code only compiles with the `DCL_RENDER_SERVER` define, which only this build sets, so the WebGL build and the `SendMessage` API above are unchanged.

### Requirements

Measured by running the same four jobs (18 stills) under Docker limits. That run was x86_64 emulated on Apple Silicon, so a native x86_64 host should do at least as well.

| | Minimum | Recommended |
|---|---|---|
| CPU | 1 x86_64 vCPU | 2 to 4 vCPU, with `LP_NUM_THREADS` set to match |
| Memory | 1 GB (512 MB is killed at startup, 768 MB after the first job) | 1.5 GB limit per worker |
| GPU | none | none |
| Disk | about 500 MB for the image | plus room for the stills |
| Network | the catalyst (`peer.decentraland.org`, or `.zone` with `env=dev`) | |

Render and encode time per still, leaving out the first still of each item:

| vCPU | Wearable on its own | Emote on the avatar | PNG encode |
|---|---|---|---|
| 1 | 560 to 950 ms | 980 to 1,080 ms | about 30 ms |
| 2 | 290 to 390 ms | 520 to 580 ms | about 30 ms |
| 4 | 160 to 270 ms | 270 to 350 ms | about 30 ms |

The first still of each new item takes 0.2 to 3 s longer: Mesa compiles each shader the first time it is used and the item's textures are uploaded. The item's second body shape does not pay it again. `MESA_SHADER_CACHE_DIR` on a volume keeps compiled shaders across restarts. An emote still also waits `--settle-frames` (0.5 s by default) before it is drawn.

### Building

1. Install the **Linux Build Support (IL2CPP)** module for the project's Unity version, and make sure Git LFS files are pulled (the avatar rig and the built-in emotes are LFS-tracked).
2. Build the player: **Decentraland > Build Render Server (Linux)**, or in batch mode:
   ```bash
   Unity -batchmode -quit -projectPath avatar-preview-renderer -executeMethod Editor.RenderServerBuild.Build
   ```
   The output goes to `Builds/RenderServer/renderer.x86_64` (override with `RENDER_SERVER_OUTPUT`). The Linux player lists Vulkan first and OpenGL second: the player rejects Mesa's CPU Vulkan device, so on a CPU server it runs on Mesa's llvmpipe OpenGL driver. The scripting backend is IL2CPP, because Mono's JIT aborts when the x86_64 image runs emulated on an ARM host. Building on an Apple Silicon Mac uses the `com.unity.sdk.linux-x86_64` and `com.unity.toolchain.macos-arm64-linux` packages; a different build host needs its own toolchain package.
3. Build the image, from `avatar-preview-renderer/`:
   ```bash
   docker build --platform linux/amd64 -f RenderServer/Dockerfile -t avatar-preview-render-server .
   ```

### From a release

Every renderer release (`avatar-preview-renderer/vX.Y.Z`) also carries `avatar-preview-render-server-vX.Y.Z-linux-x86_64.tar.gz`: the built player plus this folder's `RenderServer/`, ready to build the image anywhere:
```bash
mkdir render-server && tar xzf avatar-preview-render-server-vX.Y.Z-linux-x86_64.tar.gz -C render-server
docker build -f render-server/RenderServer/Dockerfile -t avatar-preview-render-server render-server
```
To try a branch, dispatch the **Avatar Preview Render Server** workflow; the package is attached to the run as an artifact.

### Running

Jobs from a file, exiting when done (exit code `1` if any job failed):
```bash
docker run --rm -v "$PWD/shots:/data/out" -v "$PWD/jobs.json:/jobs.json:ro" \
  avatar-preview-render-server --jobs /jobs.json --out /data/out
```

As a long-running worker, one JSON job per stdin line and one JSON result per stdout line. The Unity log goes to stderr. Unity prints a few lines of its own to stdout while it boots, so read only the lines that start with `{`, or pass `--results <file>` to get the results on their own:
```bash
docker run --rm -i -v "$PWD/shots:/data/out" avatar-preview-render-server --serve --out /data/out --max-jobs 500
```

| Option | Default | |
|---|---|---|
| `--serve` / `--jobs <file>` | | Read jobs from stdin, or from a file holding a JSON array or one job per line. |
| `--out <dir>` | | Required. Each job writes into `<dir>/<id>/`. |
| `--results <file>` | `/dev/fd/3` | Where the JSON result lines go. A regular file is appended to. The player redirects its own stdout into its log, so outside the image either open file descriptor 3 (e.g. `3>&1`) or pass a file. The image's entrypoint points descriptor 3 at the container's stdout. |
| `--size` | `1024` | Width and height of every PNG. The image's entrypoint also sizes the virtual screen from `RENDER_SERVER_SIZE`. |
| `--render-scale` | `1` | URP render scale. `1` draws at the output size; `2` supersamples each still from a render at twice the size, for smoother edges at about 4× the CPU cost. |
| `--timeout` | `90` | Seconds a single load may take. When exceeded, the process reports the job and exits with code `3`. |
| `--max-jobs` | `0` | In `--serve`, exit cleanly after this many jobs so a supervisor can recycle the process. `0` never does. |
| `--settle-frames` | `15` | Frames an emote pose is held before capture, so spring bones come to rest. |
| `--male-profile` / `--female-profile` | `default2` / `default1` | Profiles used for each body shape. The item is shown on its own, but the body shape still comes from the profile. |

### Jobs

```json
{ "urn": "urn:decentraland:matic:collections-v2:0x...:0", "yaws": [0, 90, 180, 270] }
{ "urn": "urn:decentraland:matic:collections-v2:0x...:1", "times": [0.2, 0.5, 0.8] }
```

- `urn`: required. Wearables and facial features are shot as the item on its own, and emotes worn by the avatar.
- `id`: names the output folder. Defaults to the urn with unsafe characters replaced by `_`.
- `yaws`: degrees about the vertical axis. Defaults to `[0, 90, 180, 270]` for wearables and `[0]` for emotes.
- `pitch`: tilt of the item on its own, clamped like a drag.
- `times`: emotes only. Fractions (0 to 1) of the emote's length. Defaults to `[0.2, 0.5, 0.8]`.
- `bodyShapes`: `["male"]`, `["female"]` or both. Left out, both shapes are shot when their representations differ, a single `unisex` set when they are the same files, and only the shapes an item has otherwise.
- `params`: extra [parameters](#parameters) as a query string, e.g. `"env=dev&glow=on"`. Stills default to a transparent background with no shadow or glow. `mode`, `type`, `profile`, `bodyShape`, `urn`, `base64`, `contract`, `item` and `token` are set by the server and rejected here.

Each still reports `renderMs` (drawing and reading the pixels back) and `encodeMs` (PNG encode and write). Each job reports `loadMs` and `stillsMs`, with the CPU time each used as `loadCpuMs` and `stillsCpuMs`. That CPU time counts every thread, so it can be higher than the wall time.

Files are named `<bodyShape>_yaw<deg>.png` for wearables, and `<bodyShape>_t<percent>.png` for emotes (with `_yaw<deg>` appended when several yaws are requested). Each result line looks like:

```json
{"id":"...","urn":"...","ok":true,"type":"emote","files":[{"path":"<id>/male_t50.png","bodyShape":"male","yaw":0.0,"pitch":0.0,"time":0.5,"seconds":1.41,"renderMs":307.0,"encodeMs":33.0}],"ms":7138,"loadMs":1126.9,"loadCpuMs":640.0,"stillsMs":5785.6,"stillsCpuMs":8640.0}
```
