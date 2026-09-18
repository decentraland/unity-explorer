# Local Linux development build

Install the Unity version in `Explorer/ProjectSettings/ProjectVersion.txt` with
Linux Build Support and resolve the project's dependencies. Run from the repository root:

```bash
DCL_BUILD_OUT=/absolute/path/to/build/decentraland-explorer.x86_64 \
  /path/to/Unity -batchmode -projectPath "$PWD/Explorer" \
  -buildTarget StandaloneLinux64 \
  -executeMethod Editor.LinuxPlayerBuild.BuildDevelopment \
  -logFile /absolute/path/to/linux-build.log
```

Use a dedicated build output directory. The method builds the enabled Build Settings
scenes as a development player, returning exit code 0 on success and 1 on failure.
Do not add `-quit`: the method exits after the build. A missing output path, wrong
active target, unavailable build module or empty scene list fails before building.

The method uses the configured scripting backend, version, defines and graphics APIs;
it does not override them or run Cloud Build release configuration. Save Project is
invoked before building, matching the existing source-generator preparation step.
This is a local development build, not a release artifact. A working Linux player
still requires the platform's native plugins and rendering dependencies.
