using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Result of the private world access check flow.
    /// </summary>
    public enum WorldAccessResult
    {
        Allowed,
        Denied,
        PasswordCancelled,
        CheckFailed
    }

    /// <summary>
    /// Checks whether the current user has access to a private world,
    /// showing popups (password / access-denied) when needed.
    /// Implemented by PrivateWorldAccessHandler.
    /// </summary>
    public interface IWorldAccessGate
    {
        /// <summary>
        /// Runs the full access check: fetches permissions, shows UI if required, validates password.
        /// </summary>
        /// <param name="worldName">The world name (e.g. "my-world.dcl.eth")</param>
        /// <param name="ownerAddress">Optional fallback owner address for popup display</param>
        /// <param name="ct">Cancellation token — cancelled on timeout or when the caller navigates away</param>
        /// <returns>The access result the caller should act on</returns>
        UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, CancellationToken ct);
    }
}
