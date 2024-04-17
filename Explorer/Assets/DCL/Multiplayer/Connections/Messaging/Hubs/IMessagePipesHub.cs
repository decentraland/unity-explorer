using DCL.Multiplayer.Connections.Messaging.Pipe;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public interface IMessagePipesHub : IDisposable
    {
        IMessagePipe ScenePipe();

        IMessagePipe IslandPipe();

        class Fake : IMessagePipesHub
        {
            public IMessagePipe ScenePipe() =>
                IMessagePipe.Null.INSTANCE;

            public IMessagePipe IslandPipe() =>
                IMessagePipe.Null.INSTANCE;

            public void Dispose()
            {
            }
        }
    }
}
