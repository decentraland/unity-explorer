using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.ThirdParty
{
    public interface IThirdPartyNftProviderSource
    {
        UniTask<IReadOnlyList<ThirdPartyNftProviderDefinition>> GetAsync(ReportData reportData, CancellationToken ct);
    }
}
