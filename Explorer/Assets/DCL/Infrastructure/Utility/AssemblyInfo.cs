// This assembly must compile with zero compiler diagnostics: any message emitted for
// Utility.dll makes Unity's assembly updater read Utility.mvfrm, which crashes the
// incremental macOS Cloud Build. That is why csc.rsp here carries -nullable:annotations
// instead of -nullable:enable, plus -nowarn:0168.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SceneLifeCycle.Tests")]
[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
