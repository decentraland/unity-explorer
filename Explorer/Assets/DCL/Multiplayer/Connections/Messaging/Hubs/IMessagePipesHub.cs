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
                IMessagePipe.Null.Instance;

            public IMessagePipe IslandPipe() =>
                IMessagePipe.Null.Instance;

            public void Dispose()
            {
            }
        }
    }
}
