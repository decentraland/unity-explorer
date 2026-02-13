using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
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

        UniTask<Result<MetaData>> MetaDataAsync(MetaData.Input input, CancellationToken token);

        bool MetadataIsDirty { get; }
    }

    public static class SceneRoomMetaDataSourceExtensions
    {
        public static SceneRoomLogMetaDataSource WithLog(this ISceneRoomMetaDataSource origin) =>
            new (origin);
    }
}
