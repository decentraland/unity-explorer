using DCL.Multiplayer.Connections.Messaging.Pipe;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public interface IMessagePipesHub : IDisposable
    {
        IMessagePipe ScenePipe();

        IMessagePipe IslandPipe();
    }
}
