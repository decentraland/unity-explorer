using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public interface ISceneRoomMetaDataSource
    {
        /// <summary>
        ///     Scenes communication is isolated when there is an individual LiveKit room for each scene
        /// </summary>
        bool ScenesCommunicationIsIsolated { get; }

        MetaData.Input GetMetadataInput();

        UniTask<MetaData> MetaDataAsync(MetaData.Input input, CancellationToken token);

        bool MetadataIsDirty { get; }
    }
}
