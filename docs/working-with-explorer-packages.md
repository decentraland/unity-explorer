# Working with Explorer Packages

**Warning:** This process does need some improvement.

[https://github.com/decentraland/unity-explorer-packages](https://github.com/decentraland/unity-explorer-packages) is a repository used to store private Unity packages.

Access requires membership in the Decentraland GitHub organization or being manually added to it.

Due to its private nature, there are a few important nuances when working with packages from this repo.

---

## GitHub Actions: `tests.yml`

See: [unity-explorer/.github/workflows/test.yml](https://github.com/decentraland/unity-explorer/blob/dev/.github/workflows/test.yml)

Tests require packages to be copied locally at build time, due to GitHub Action permission constraints.

We do this using a `find & replace` step that affects `Explorer/Packages/manifest.json`:

```powershell
-replace 'git@github.com:decentraland/unity-explorer-packages.git?path=/StylizedGrassShader', 'file:../../unity-explorer-packages/StylizedGrassShader'
```

Since the entire repo is cloned during the workflow, all package folders are available locally. The example above replaces the Git-based path to `StylizedGrassShader` with a local file path.

**Important:**
If you add, remove, or rename packages in `manifest.json`, you **must** also update `test.yml` accordingly, or CI will fail when resolving package dependencies.


---

## Testing Feature Branches

To test changes in a package without merging them to `main`, you can point `manifest.json` to a specific feature branch:

```json
"com.decentraland.stylizedgrassshader": "git@github.com:decentraland/unity-explorer-packages.git?path=/StylizedGrassShader#feat/your_feat_branch"
```

If you do this, remember to update `test.yml` to match your branch as well -- otherwise tests will break.

---

## Merge Workflow

Packages in `manifest.json` are pinned by Git commit hash. Even if you merge changes into `unity-explorer-packages`, nothing changes in `dev` unless you explicitly update the hash.

**Recommended flow:**

1. **Develop & Test**
   Create a feature branch in `unity-explorer-packages`. Point `manifest.json` to it while testing in Unity.

2. **Merge the Package**
   Once validated, merge your feature branch into `main` in `unity-explorer-packages`.
   **Warning:** `dev` still uses the previous commit hash.
   **Warning:** `tests.yml` may start failing until you merge & update your commit hash.

3. **Update Manifest**
   Update `manifest.json` and `packages-lock.json` to point to the latest commit hash on `main` (in unity-explorer-packages)

4. **Merge to Dev**
   Once `manifest.json` points to the correct commit, merge your Unity project branch to `dev`.
