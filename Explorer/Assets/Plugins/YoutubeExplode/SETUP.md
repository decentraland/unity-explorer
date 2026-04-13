# YoutubeExplode Plugin

YouTube URL resolver for AVPro Video integration.

## DLLs

- **YoutubeExplode.dll** (v6.5.7) - YouTube video metadata and stream extraction
- **AngleSharp.dll** (v1.4.0) - HTML parser (YoutubeExplode dependency)
- **System.Text.Encoding.CodePages.dll** (v6.0.0) - Encoding support (AngleSharp dependency)

## Transitive Dependencies (already in project)

These are required by YoutubeExplode but already exist in `Plugins/Shared/` and `Plugins/SocketIO/libdeps/`:
- System.Memory
- System.Text.Json
- Microsoft.Bcl.AsyncInterfaces
- System.Threading.Tasks.Extensions

## Source

All DLLs are `netstandard2.0` targets from NuGet.org.
