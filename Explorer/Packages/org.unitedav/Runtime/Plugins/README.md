# UnitedAV native plugins

The C# runtime (`UnitedAV.Runtime`) P/Invokes a native library whose base name is
`UnitedAV` (see `Runtime/UnitedAV/Internal/UnitedAVNative.cs`). At load time Mono /
IL2CPP resolve the platform-specific file name from that base name:

| Platform        | Resolved file name     |
|-----------------|------------------------|
| Linux x86_64    | `libUnitedAV.so`       |
| Windows x86_64  | `UnitedAV.dll`         |
| macOS universal | `libUnitedAV.dylib`    |

## Build from source

The native plugin is **not committed** — build it and place the artifact under a
per-platform folder, each with a Unity `PluginImporter` `.meta` that enables it for
exactly the platforms it targets:

```
Runtime/Plugins/
  Linux/x86_64/libUnitedAV.so        # Editor + Standalone Linux x86_64
  Windows/x86_64/UnitedAV.dll        # Editor + Standalone Windows x86_64
  macOS/UnitedAV.dylib               # Editor + Standalone macOS
```

Built binaries are git-ignored. To produce the Linux artifact:

```sh
cmake -S native -B native/build -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build native/build
mkdir -p unity/Runtime/Plugins/Linux/x86_64
cp native/build/libUnitedAV.so unity/Runtime/Plugins/Linux/x86_64/libUnitedAV.so
```
