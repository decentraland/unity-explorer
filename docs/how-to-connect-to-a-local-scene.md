# How to Connect to a Local Scene (Unity Editor / Custom Build / Latest Released Build)

## Installing NodeJS

1. Download the installer from here: [https://nodejs.org/en](https://nodejs.org/en)
2. Press next in all steps. If it asks you whether to "install tools for native modules", install it too.
3. Open a console (cmd or powershell) and type `node -v` to check that it is correctly installed.

## Running the scene

**1. Download the desired SDK7 scene:** e.g. [SDK7 Scene Template](https://github.com/decentraland/sdk7-scene-template) / [Any scene from SDK7 Test Scenes](https://github.com/decentraland/sdk7-test-scenes/blob/main/scenes/54,-55-Testing-3d-models/) / [Any scene from the Goerli Plaza Test Scenes](https://github.com/decentraland/sdk7-goerli-plaza/tree/main/Shark-animation). This is the source code of the scene you will modify and run.

**2. Run the scene locally:** Open a console at the root of the scene folder and run `npm i` (alternatively if you run `npm i @dcl/sdk@latest` you can make sure you are using the latest SDK released version). Once it finishes, execute `npm run start -- --explorer-alpha` (on Windows it may need to be `npm run start '--' --explorer-alpha`). This will create the compiled bin file in `sdk7-scene-template/bin/`.

**3. The Latest Released Build of the Explorer opens:** The command ran in step 2 ends up opening the **Latest Released Build** (that gets downloaded by the launcher) and connects it to the locally running scene. If you want to test the scene with the latest released build of the Explorer then next steps are not needed.

**4. Close the Launcher / Explorer that auto-opened:** Since you will be connecting to it with Unity Editor or a custom build. Keep the console open.

**Note: Modify the scene's code to your liking:** Modify the code files inside `sdk7-scene-template/src/` according to your needs and it will be automatically updated on the connected Explorer. Remember that you have the [SDK Documentation](https://docs.decentraland.org/creator/) available to learn how to implement each component from the SDK side.

### (Optional) Run a local test environment with several scenes (AKA SDK Workspace)

For example you could be interested in testing some scenes from the [SDK Goerli Plaza Test Scenes](https://github.com/decentraland/sdk7-goerli-plaza) and you want to run some/all of them together.

1. Clone the [sdk7-goerli-plaza](https://github.com/decentraland/sdk7-goerli-plaza) repo.
2. Modify the `dcl-workspace.json` file in order to have ONLY the list of scene paths that you want to test. For example:

```json
{
  "folders": [
    {
      "path": "advanced-avatar-swap"
    },
    {
      "path": "avatar-swap"
    },
    {
      "path": "Cube"
    }
  ]
}
```

3. In the console, run `npm i` and then `npm run start -- --explorer-alpha` (on Windows it may need to be `npm run start '--' --explorer-alpha`) to run the workspace locally.

## Connecting Unity Editor to the scene

1. Set the Main Scene Loader -> Startup Config -> Initial Realm to "Localhost".
2. Set the initial position to one of the locally running scene.
3. Toggle the "Use Remote Asset Bundles" according to your need. If it's off then the original raw GLTFs from the locally running scene will be loaded instead of downloading their asset bundle version (if it exists).
4. Hit PLAY and the editor should connect to the locally running scene.

## Connecting a custom build to the scene

Custom builds can be just locally built executables or for example the PR builds exposed by the CI.

Run the build from a console/terminal specifying the needed parameters, for example:

### Windows

```
"C:\Users\[YOUR-USER]\Downloads\Decentraland_windows64\Decentraland.exe" --realm http://127.0.0.1:8000 --position 0,0 --local-scene true --debug --skip-version-check true
```

### macOS

```
open Decentraland.app --args --realm http://127.0.0.1:8000 --position 0,0 --local-scene true --debug --skip-version-check true
```

## Modifying the scene

You can work with the scene using an editor like VSCode as explained here: [SDK 101](https://docs.decentraland.org/creator/development-guide/sdk7/sdk-101/)

If you change the source code of the scene (subfolder `src`, `index.ts` for example) you should see how those changes are replicated in Unity after some seconds.

If you want to change other data, like the parcels of the scene, you would modify the `scene.json` file and then stop the scene in the console (press Control+C in the console), and run it again.
