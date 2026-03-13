# Troubleshooting Missing UnityCI Docker Images

This page documents the resolution for the `manifest unknown` error encountered when a specific Unity Editor version or target platform is missing from the official GameCI Docker Hub registry.

---

## The Problem: "Manifest Unknown"

When running CI/CD pipelines (like `unity-test-runner`), the process may fail during the **"Download from Docker Hub"** step. This happens because the requested Unity version/platform combination has not been built or cached by the upstream provider.

**Error Log Example:**
> `Error response from daemon: manifest for unityci/editor:ubuntu-6000.3.9f1-linux-il2cpp-3.2.0 not found: manifest unknown`

Because the image doesn't exist, subsequent steps like **Report test results** and **upload-artifact** also fail, as no environment was available to run the tests or generate reports.

---

## The Fix: Manual Image Builder Workflow

To resolve this, we have implemented a dedicated GitHub Action to manually build and push the required Unity CI images to our internal GitHub Container Registry (GHCR).

### Workflow Location
`Build GameCI Image` (`.github/workflows/build-gameci-image.yml`)

### How to Build a Missing Image
1. Go to the **Actions** tab in the repository.
2. Select the **Build GameCI Image** workflow from the sidebar.
3. Click **Run workflow** and fill in the following parameters:
    * **Unity Editor Version:** (e.g., `6000.2.14f1`)
    * **Unity Editor Changeset:** The unique hash for that version (found on the [Unity Download Archive](https://unity.com/releases/editor/archive)).
    * **Base OS:** Usually `ubuntu`.
    * **Target Platform:** The module you need (e.g., `linux-il2cpp`, `android`, `webgl`).
    * **Image Tag Suffix:** Standard versioning for the runner (e.g., `3.2.0`).

### What this Workflow Does
1. **Clones** the official `game-ci/docker` repository to get the latest Dockerfiles.
2. **Authenticates** with our GHCR using the `GITHUB_TOKEN`.
3. **Builds** the image locally on the runner using the provided `build-args`.
4. **Pushes** the image to: `ghcr.io/decentraland/unityci-editor:[TAG]`

---

## Implementation Details

The fix utilizes a custom build script that bridges the gap when upstream images are missing:

```yaml
- name: Build and Push Docker Image
  working-directory: docker
  run: |
    IMAGE_NAME="ghcr.io/decentraland/unityci-editor:${{ inputs.base_os }}-${{ inputs.editor_version }}-${{ inputs.target_platform }}-${{ inputs.image_suffix }}"

    docker build . \
      --file ./images/${{ inputs.base_os }}/editor/Dockerfile \
      -t $IMAGE_NAME \
      --build-arg version=${{ inputs.editor_version }} \
      --build-arg changeSet=${{ inputs.change_set }} \
      --build-arg module=${{ inputs.target_platform }}

    docker push $IMAGE_NAME
```
