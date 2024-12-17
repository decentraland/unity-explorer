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

    public interface INavmapCommand<TParam> : INavmapCommand
    {
        UniTask ExecuteAsync(AdditionalParams? param, CancellationToken ct);
    }

    public struct AdditionalParams
    {
        public bool IsFromSearchResults;

        public AdditionalParams(bool isFromSearchResults)
        {
            this.IsFromSearchResults = isFromSearchResults;
        }
    }
}
