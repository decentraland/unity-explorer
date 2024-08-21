using Arch.Core;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public interface IUserInAppInitializationFlow
    {
        UniTask ExecuteAsync(
            bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            World world,
            Entity playerEntity,
            CancellationToken ct);
    }
}
