using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Navmap
{
    public interface INavmapCommand
    {
        UniTask ExecuteAsync(CancellationToken ct);

        void Undo();
    }
}
