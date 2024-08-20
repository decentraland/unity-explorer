using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    public interface IThirdPartyNftProviderSource
    {
        UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(CancellationToken ct);
    }
}
