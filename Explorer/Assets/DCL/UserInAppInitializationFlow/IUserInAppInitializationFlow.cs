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
            World world,
            Entity playerEntity,
            CancellationToken ct);
    }
}
