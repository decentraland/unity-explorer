using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public interface IMetaDataSource
    {
        UniTask<MetaData> MetaDataAsync(CancellationToken token);
    }
}
