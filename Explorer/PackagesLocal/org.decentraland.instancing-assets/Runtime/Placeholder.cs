// org.decentraland.instancing-assets ships the DCL tree/detail prototype
// assets and their shaders, not C# API — no DCL code imports from it. This
// single internal type gives DCL.GPUIAssets.asmdef a compilation unit so it
// builds into a valid assembly, which in turn makes the package present for
// DCL.Plugins.asmdef's versionDefines mapping
// (org.decentraland.instancing-assets -> GPUI_PRO_PRESENT); that symbol gates
// the tree-instancing call sites when both this package and
// org.decentraland.unityuniversalinstancer are installed.

namespace DCL.GPUIAssets
{
    internal static class Placeholder { }
}
