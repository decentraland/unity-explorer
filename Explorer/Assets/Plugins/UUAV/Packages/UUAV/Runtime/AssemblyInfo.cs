using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]

// the PlayMode suite reaches internals for two things only: PlayerId (to
// address native queries) and NativeMethods (to pump uuav_player_read_audio
// when the editor runs without an audio DSP)
[assembly: InternalsVisibleTo("UUAV.Tests")]
