using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Profiles.Publishing
{
    public interface IProfilePublishing
    {
        UniTask<bool> IsProfilePublishedAsync(CancellationToken ct);

        UniTask PublishProfileAsync(CancellationToken ct);
    }
}
