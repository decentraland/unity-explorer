using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public interface IUserInAppInitializationFlow
    {
        UniTask ExecuteAsync(
            bool showAuthentication,
            bool showLoading,
            CancellationToken ct);
    }
}
