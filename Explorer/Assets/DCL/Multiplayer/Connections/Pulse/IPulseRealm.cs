using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     The realm string Pulse announces peers under and filters incoming messages by. The server
    ///     partitions visibility by exact string match, so this value is the only thing separating two
    ///     sessions sharing a Pulse instance.
    ///     Outside local scene development it follows the current realm live; in local scene development
    ///     it is a per-dev-process key resolved once, before connecting.
    /// </summary>
    public interface IPulseRealm
    {
        /// <summary>
        ///     Empty while the realm is unknown — callers must not connect to Pulse in that state,
        ///     since an empty realm violates the server contract.
        /// </summary>
        string Value { get; }

        /// <summary>
        ///     Resolves the realm if it is not known yet. Called once before connecting; a no-op for
        ///     realms that are already known. Never throws: an unresolved realm leaves
        ///     <see cref="Value" /> empty rather than failing the caller's flow.
        /// </summary>
        UniTask EnsureResolvedAsync(CancellationToken ct);
    }
}
