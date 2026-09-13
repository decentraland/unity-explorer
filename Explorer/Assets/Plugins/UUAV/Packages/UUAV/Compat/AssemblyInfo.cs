using System.Runtime.CompilerServices;

// the PlayMode suite drives UUAVBackend directly so its tests keep the
// tap-paced player from UUAVTestBase instead of the facade's own AudioSource
[assembly: InternalsVisibleTo("UUAV.Tests")]
