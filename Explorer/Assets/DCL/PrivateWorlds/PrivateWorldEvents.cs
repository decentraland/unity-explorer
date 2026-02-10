using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Result of the private world access check flow (publisher awaits this).
    /// </summary>
    public enum WorldAccessResult
    {
        Allowed,
        Denied,
        PasswordCancelled,
        CheckFailed
    }

    /// <summary>
    /// Event for private world access check. RealmNavigator publishes; PrivateWorldAccessHandler subscribes.
    /// </summary>
    public readonly struct CheckWorldAccessEvent
    {
        public readonly string WorldName;
        public readonly string? OwnerAddress;
        public readonly CancellationToken CancellationToken;
        public readonly UniTaskCompletionSource<WorldAccessResult> ResultSource;

        public CheckWorldAccessEvent(
            string worldName,
            string? ownerAddress,
            UniTaskCompletionSource<WorldAccessResult> resultSource,
            CancellationToken cancellationToken)
        {
            WorldName = worldName;
            OwnerAddress = ownerAddress;
            ResultSource = resultSource;
            CancellationToken = cancellationToken;
        }
    }
}
