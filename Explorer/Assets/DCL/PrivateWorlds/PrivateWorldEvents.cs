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
        UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, CancellationToken ct);
    }
}
