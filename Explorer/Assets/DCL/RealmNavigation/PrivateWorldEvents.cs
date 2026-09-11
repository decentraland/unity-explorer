using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.PrivateWorlds
{
    public enum WorldAccessResult
    {
        Allowed,
        Denied,
        PasswordCancelled,
        CheckFailed
    }

    public interface IWorldAccessGate
    {
        /// <summary>
        ///     Checks access to a world. <paramref name="realm" /> is the exact realm URL being navigated to;
        ///     any validated password is scoped to it.
        /// </summary>
        UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, URLDomain realm, CancellationToken ct);
    }

    public interface ICommunityMembershipChecker
    {
        UniTask<bool> IsMemberOfCommunityAsync(string communityId, CancellationToken ct);
    }
}
