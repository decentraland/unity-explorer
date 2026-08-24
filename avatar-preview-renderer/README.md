# Aang Renderer

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
* `profile`
* `urn` or `contract` & `item` or `contract` & `token`
  * Multiple urn parameters may be used to preview several items at once (a cart, an outfit). The remaining categories are still filled from the profile, and the passed urns override by category, so the last urn wins its category. Several items can only be shown worn by the avatar, since the item-alone view renders a single item.
* `emote`
* `type` (optional)
* `disableSwitcher` (optional)

### Profile
* `background`
* `profile`
* `emote`

### Authentication
* `background`
* `profile`
* `emote`

### Builder
* `background`
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
