# App Arguments

This document describes all available application argument flags (AppArgsFlags) that can be passed to the Decentraland Unity Explorer. These flags control various features, behaviors, and configurations during application startup.

## Usage

Flags can be passed via command line arguments using the format:
```bash
--flag-name
--flag-name value
```

Or embedded in deep links:
```
decentraland://?flag-name=value&other-flag=true
```
For embedded links you will need to place value after `=` sign, instead of space.

---

## Flags

### `debug`
**Description:** Enables debug mode. When set, the application runs in debug mode. This flag is automatically added when running in Unity Editor. When enabled, many debug features and development tools become available.

**Usage:**
```bash
--debug
```

---

### `hub`
**Description:** Indicates that the application is running from the DCL Editor (Creator Hub). Used for analytics tracking to distinguish between Unity Editor, DCL Editor, debug builds, and release builds.

**Usage:**
```bash
--hub
```

---

## Version & System Checks

### `skip-version-check`
**Description:** Skips the version check that normally runs on startup. Useful for development and testing scenarios where version validation should be bypassed.

**Usage:**
```bash
--skip-version-check
```

---

### `simulateVersion`
**Type:** String
**Description:** Simulates a specific version number for testing purposes. Overrides the actual version detection.

**Usage:**
```bash
--simulateVersion 1.0.0
```

---

### `forceMinimumSpecsScreen`
**Description:** Forces the minimum system specifications screen to be displayed, regardless of the actual system capabilities. Useful for testing the minimum specs screen UI.

**Usage:**
```bash
--forceMinimumSpecsScreen
```

---

## Scene & Environment Flags

### `scene-console`
**Description:** Enables the scene console for debugging and development. Only available in debug mode or when running local scenes.

**Usage:**
```bash
--scene-console
```

---

### `dclenv`
**Type:** String
**Description:** Sets the Decentraland environment (e.g., `org`, `zone`, `today`). Determines which API endpoints and services the application connects to.

**Usage:**
```bash
--dclenv org
```

---

### `realm`
**Type:** String (URL)
**Description:** Specifies a custom realm server URL to connect to. Used for connecting to local or custom Decentraland servers. The URL should include the protocol (http:// or https://).

**Usage:**
```bash
--realm=http://127.0.0.1:8000
--realm=https://peer-ap1.decentraland.zone/
```

---

### `local-scene`
**Type:** Bool
**Description:** Enables local scene development mode.

**Usage:**
```bash
--local-scene true
```

---

### `position`
**Type:** String (coordinates)
**Description:** Sets the initial spawn position in the world. Format is typically `x,y` coordinates.

**Usage:**
```bash
--position 100,100
```

---

## Authentication Flags

### `skip-auth-screen`
**Description:** Skips the authentication screen on startup. When set, the user bypasses the login/auth flow.

**Usage:**
```bash
--skip-auth-screen
```

---

## Avatar & Profile Flags

### `self-force-emotes`
**Type:** String
**Description:** Forces specific emotes to be available for preview. Accepts a comma-separated list of emote URNs (i.e. `urn:decentraland:matic:collections-v2:0xa80aea22d0fe9d34ca72ce304ef427bbefee1f11:2` ).

The elements previewed are not visible for others, only for the tester.

Only works for PUBLISHED elements (thus having a URN that identifies them).

**Usage:**
```bash
--self-force-emotes emote1,emote2,emote3
```

---

### `self-preview-emotes`
**Type:** String
**Description:** Enables preview mode for specific emotes. Accepts a comma-separated list of emote URNs (i.e. `urn:decentraland:matic:collections-v2:0xa80aea22d0fe9d34ca72ce304ef427bbefee1f11:2` ) that will be available for preview.

The elements previewed are not visible for others, only for the tester.

Only works for PUBLISHED elements (thus having a URN that identifies them).

**Usage:**
```bash
--self-preview-emotes emote1,emote2
```

---

### `self-preview-wearables`
**Type:** String
**Description:** Enables preview mode for specific wearables. Accepts a comma-separated list of wearable URNs (i.e. `urn:decentraland:matic:collections-v2:0xc11b9d892e12cfaca551551345266d60e9abff6e:3` )

The elements previewed are not visible for others, only for the tester.

Only works for PUBLISHED elements (thus having a URN that identifies them).

**Usage:**
```bash
--self-preview-wearables wearable1,wearable2
```

---

### `self-preview-builder-collections`
**Type:** String
**Description:** Enables preview mode for builder collections. Accepts a comma-separated list of collection IDs (e.g. `3062136a-065d-4d94-b28c-f57d6ef04860`).

The elements previewed are not visible for others, only for the tester.

Only works for UNRELEASED elements, the tester has to either be the owner of the collection or be whitelisted to test it (e.g. Curators).

Make sure to use the COLLECTION ID (from the collection URL) and not the ITEM ID (from each item URL).

More detailed instructions on how to test can be found in the description of relevant PRs that have worked on the usage of this flag, for example https://github.com/decentraland/unity-explorer/pull/5309

**Usage:**
```bash
--self-preview-builder-collections collection1,collection2
```

---

## Performance & Caching Flags

### `disable-disk-cache`
**Description:** Disables the disk cache system. All cached assets will be loaded from network or memory instead. Useful for testing cache-related issues or ensuring fresh data loads.

**Usage:**
```bash
--disable-disk-cache
```

---

### `disable-disk-cache-cleanup`
**Description:** Disables automatic cleanup of the disk cache. Prevents the cache from being automatically cleared or managed.

**Usage:**
```bash
--disable-disk-cache-cleanup
```

---

### `simulateMemory`
**Type:** String (integer)
**Description:** Simulates a specific amount of system memory (in MB). Overrides the actual system memory detection. Useful for testing memory-related features and constraints.

**Usage:**
```bash
--simulateMemory 4096
```

---

## Development Tools Flags

### `identity-expiration-duration`
**Type:** String (integer, seconds)
**Description:** Sets the duration (in seconds) before user identity expires. Overrides the default identity expiration time.

**Usage:**
```bash
--identity-expiration-duration 3600
```

---

### `launch-cdp-monitor-on-start`
**Type:** Boolean
**Description:** Launches the Chrome DevTools Protocol (CDP) monitor on application start. Enables remote debugging capabilities.

**Usage:**
```bash
--launch-cdp-monitor-on-start
```

---

### `creator-hub-bin-path`
**Type:** String (file path)
**Description:** Specifies a custom path to the Creator Hub binary. Used when the Creator Hub needs to be launched from a non-standard location.

**Usage:**
```bash
--creator-hub-bin-path /path/to/creator-hub
```

---

### `use-log-matrix`
**Type:** String (file path)
**Description:** Enables logging to a matrix file. The value should be the path to the log matrix file.

**Usage:**
```bash
--use-log-matrix /path/to/log-matrix.txt
```

---

## Display & Window Flags

### `windowed-mode`
**Type:** Boolean
**Description:** Forces the application to run in windowed mode instead of fullscreen.

**Usage:**
```bash
--windowed-mode
```

---

## Feature Flags Configuration

### `feature-flags-url`
**Type:** String (URL)
**Description:** Overrides the default feature flags service URL. Used to connect to a custom feature flags server.

**Usage:**
```bash
--feature-flags-url https://custom-feature-flags.example.com
```

---

### `feature-flags-hostname`
**Type:** String
**Description:** Overrides the hostname used for feature flags requests. Used for custom feature flag configurations.

**Usage:**
```bash
--feature-flags-hostname my-custom-hostname
```

---

## Analytics Flags

### `session_id`
**Type:** String
**Description:** Sets a custom session ID for analytics tracking. Overrides the automatically generated session ID.

**Usage:**
```bash
--session_id=abc123xyz
```

---

### `launcher_anonymous_id`
**Type:** String
**Description:** Sets the launcher anonymous ID for analytics tracking. Used to link analytics data between the launcher and the explorer.

**Usage:**
```bash
--launcher_anonymous_id user123
```

---

## Notes

- Most boolean flags are presence flags (they don't require a value). Simply including `--flag-name` enables the feature.
- Some flags accept string values that can be boolean-like (`"true"` or `"false"`).
- Flags can be combined in a single command line invocation.
- Deep links can embed multiple flags: `decentraland://?realm=http://127.0.0.1:8000&local-scene=true&skip-auth-screen=true`
- The `debug` flag is automatically added when running in Unity Editor.
- Some flags are only effective when combined with the `debug` flag or when running in Unity Editor.

---

## Example Usage

### Local Scene Development
```bash
--local-scene --skip-auth-screen true --position 100,100 --debug
```

### Custom Realm with Debug Features
```bash
--realm=http://127.0.0.1:8000 --debug --scene-console --windowed-mode
```

### Testing with Simulated Memory
```bash
--simulateMemory 2048 --disable-disk-cache --debug
```

### Preview Mode for Wearables
```bash
--self-preview-wearables wearable1,wearable2,wearable3 --debug
```
