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
            private readonly IMessagePipe fake = new IMessagePipe.Fake();

            public IMessagePipe ScenePipe() =>
                fake;

            public IMessagePipe IslandPipe() =>
                fake;

            public void Dispose()
            {
            }
        }
    }
}
