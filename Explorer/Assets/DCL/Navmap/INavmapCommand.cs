using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Navmap
{
    public interface INavmapCommand : IDisposable
    {
        UniTask ExecuteAsync(CancellationToken ct);

        void Undo();
    }
}
