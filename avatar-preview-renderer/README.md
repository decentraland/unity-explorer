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
* `hideControls`: Hides every in-canvas control: the switcher and the emote play / mute buttons. For embedders that draw their own controls around the canvas or capture it for thumbnails. The loader is separate (`disableLoader`), and mouse rotation, wheel zoom and right-drag pan keep working. Can be toggled at runtime with `SetHideControls` without a reload. Default is `false`.
* Camera options, named and scaled as in the Babylon preview so an app frames both renderers with the same values. Every one of them can also be set at runtime without a reload (see [Camera](#camera) below). Used in `marketplace` and `builder` mode, the two that take camera input.
  * `zoom`: `0` to `100`, the closest view the wheel can reach: `0` is the fitted view, `100` is 2.8 times closer. When neither `zoom` nor `wheelZoom` is passed, the wheel moves between two thirds of the fitted view and twice it, starting at the fit.
  * `wheelZoom`: how far the wheel pulls back from the `zoom` view, as a factor. `1` (the default when `zoom` is passed) leaves no wheel zoom at all.
  * `wheelStart`: `0` to `100`, where the view starts in that range: `100` is the closest view, `0` the farthest. Default is `50`.
  * `camera`: `interactive` (default) or `static`. A static camera takes no rotation, zoom or pan, never auto-rotates, and holds the emote on its first frame.
  * `lockAlpha`, `lockBeta`, `lockRadius`: lock the turntable, the tilt and the wheel zoom respectively. Default is `false`.
  * `panning`: whether right-drag pans the view. Default is `true`.
  * `disableAutoRotate`: stops the idle turntable in every view. Default is `false`.
  * `autoRotateSpeed`: the idle turntable speed in radians per second. Default is `0.2`.
  * `offsetX`, `offsetY`: where the view starts, in metres across and up the screen. Default is `0`.
  * `showThumbnailBoundaries`: outlines the centred half of the canvas that a square screenshot of it keeps, for the Builder's thumbnail editor. Default is `false`.
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
* the camera options (optional)

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
* the camera options (optional)

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

### Camera

These act on the live view and need no `Reload`. A reload within the same mode keeps the zoom and pan the user set; switching modes, or switching between the avatar and the item view, starts again from the options.

* `SetZoomLevel`, `SetWheelZoom`, `SetWheelStart`: the URL options above, applied at once. The view restarts at the new range's start position, as it would after a reload. An empty `SetZoomLevel` or `SetWheelZoom` unsets the option.
* `SetCamera`, `SetLockAlpha`, `SetLockBeta`, `SetLockRadius`, `SetPanning`, `SetDisableAutoRotate`, `SetAutoRotateSpeed`, `SetShowThumbnailBoundaries`: the URL options above, applied at once.
* `SetZoom`: Babylon's `changeZoom`. A delta that pulls the view closer when positive; the Builder's zoom buttons send `0.1` and `-0.1`, which move the orbit by one metre of Babylon's 3.5 m camera distance.
* `SetOffset`: Babylon's `panCamera`. `x,y,z` in metres, across and up the screen, replacing any right-drag pan. `z` is accepted and ignored. The Builder's thumbnail slider drives `y`.
* `SetCameraPosition`: Babylon's `changeCameraPosition`. `alpha,beta,radius` deltas: alpha turns the subject and beta tilts it, in radians, and radius pulls the orbit back by metres.

```javascript
unityInstance.SendMessage('JSBridge', 'SetZoom', '0.1');
unityInstance.SendMessage('JSBridge', 'SetOffset', '0,-0.5,0');
unityInstance.SendMessage('JSBridge', 'SetCamera', 'static');
```

## Replies from the renderer

Every reply is posted to the renderer's own window when it is embedded in an iframe (or to `window.parent` otherwise) as:

```javascript
{ type: 'unity-renderer', payload: { type: '<reply type>', payload: <value> } }
```

The reply types are `loaded`, `error`, `screenshot`, `metrics`, `request-failed`, `emoteLength`, `isEmotePlaying`, `hasSound`, `element-bounds`, `customization-done` and `avatar-customization-step`.

A request the renderer cannot serve answers with `request-failed` instead of its normal reply, so a caller never waits forever:

```javascript
{ type: 'request-failed', payload: { request: 'screenshot' | 'metrics', reason: '<why>' } }
```

Today that happens for a sized screenshot or a metrics request made while a reload is in flight (nothing is rendered during a reload), for metrics when none of the requested items is loaded, for a sized screenshot whose size is not `width,height` within 1 to 4096 per side or would exceed the GPU texture limit at the current render scale (small canvases render at twice the size), and for a screenshot whose readback failed. An unsized screenshot during a reload still captures the canvas as it is.

## Taking screenshots

The renderer has the ability to take a screenshot and provide the png as a base64 encoded string.

```javascript
unityInstance.SendMessage('JSBridge', 'TakeScreenshot', '');          // the canvas as it is on screen
unityInstance.SendMessage('JSBridge', 'TakeScreenshot', '1024,1024'); // rendered at exactly this size
```

With no size, the capture is the canvas at its on-screen pixel size, controls included. With `width,height` (each from 1 to 4096), the view is rendered offscreen at exactly that size: the vertical framing is the live one and the horizontal extent follows the requested aspect, so a square capture of a wide canvas is its centre. Nothing drawn by the in-canvas UI is part of it. The alpha channel follows the `background` parameter either way.

The method takes one parameter, so always pass a value: a `SendMessage` with no value at all gets no reply.

The reply is a `screenshot` with the PNG as base64:

```javascript
window.addEventListener('message', (event) => {
  if (event.data?.type !== 'unity-renderer') return;
  const { type, payload } = event.data.payload;
  if (type === 'screenshot') console.log('Received screenshot:', payload);
  if (type === 'request-failed' && payload.request === 'screenshot') console.warn(payload.reason);
});
```

## Reading metrics

`GetMetrics` reports the geometry of the items the caller asked for (the `urn`, `base64`, `contract` + `item` / `token` ones), never the profile's own wearables or the body:

```javascript
unityInstance.SendMessage('JSBridge', 'GetMetrics');
```

The reply is a `metrics` object:

```javascript
{ triangles, materials, textures, meshes, bodies, entities }
```

* `triangles`: the sum over every renderer of the items, colliders (nodes with `collider` in their name) excluded.
* `materials` and `textures`: as declared in the items' glTF files, so the renderer's own material conversion never changes them.
* `meshes` and `bodies`: one per glTF primitive (a Unity sub-mesh), colliders left out as the Babylon preview drops them before counting. Babylon also counts its container nodes, so its number can be a little higher for the same file.
* `entities`: how many of the requested items are loaded. An emote contributes its prop's geometry, if it has one; a facial feature contributes its textures.
