# Unity Upgrades

## Game-CI

When upgrading Unity, the required Game-CI Docker images may not yet be available upstream. To unblock CI and local builds, we provide a GitHub Actions workflow that allows us to manually build and publish Unity editor images to our own GitHub Container Registry (GHCR) namespace.

This ensures we can continue upgrading Unity without waiting for official Game-CI images to be released.

## When to Use This

Use this workflow when:

- Upgrading to a newer Unity version that is not yet available via Game-CI images
- CI tests.yml fails due to missing Unity editor Docker images


## Workflow Location

The workflow lives here:

https://github.com/decentraland/unity-explorer/actions/workflows/build-game-ci-image.yml



## How the Workflow Works

The workflow:

1. Clones the official `game-ci/docker` repository
2. Builds a Unity editor Docker image using the selected inputs
3. Tags the image following our internal naming convention
4. Pushes the image to GHCR under `ghcr.io/decentraland`

These images can then be referenced by CI jobs exactly like standard Game-CI images.



## Image Naming Convention

All images are published with the following format:

```
ghcr.io/decentraland/unityci-editor:<base_os>-<editor_version>-<target_platform>-<image_suffix>
```

Example:

```
ghcr.io/decentraland/unityci-editor:ubuntu-6000.2.14f1-linux-il2cpp-3.2.0
```



## Running the Workflow Manually


<img width="417" height="647" alt="image" src="https://github.com/user-attachments/assets/b4693d4f-c7a2-4c78-8a78-8adfd597cca5" />


1. Go to **Actions -> Build GameCI Image**
2. Click **Run workflow**
3. Fill in the inputs:

### Inputs

- **Editor Version**
  Unity editor version (e.g. `6000.2.14f1`)

- **Change Set**
  Unity changeset hash for that editor version
  This must match the exact Unity release

- **Base OS**
  OS used for the Docker image
  Usually `ubuntu`

- **Target Platform**
  Unity module to install
  Common values:
  - `linux-il2cpp`
  - `windows-il2cpp`
  - `mac-il2cpp`
  - `android`
  - `ios`
  - `webgl`

- **Image Tag Suffix**
  Used to version or differentiate images
  Typically aligned with our internal Game-CI versioning

4. Run the workflow and wait for the image to build and push

The build runs on a custom runner:
This is required due to:

- Large Docker image size
- Unity editor install requirements
- Build memory usage



## Using the Image in CI

Once built and pushed, re-running the failing tests.yml will now retrieve the image from the GHCR.
