using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Profiles
{
    public interface IProfileRepository
    {
        UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct);
    }
}
