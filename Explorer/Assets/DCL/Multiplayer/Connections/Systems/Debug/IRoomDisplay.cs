using DCL.Multiplayer.Connections.Rooms.Connective;
using System;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public interface IRoomDisplay
    {
        void Update();

        class Null : IRoomDisplay
        {
            public void Update()
            {
                //ignore
            }
        }
    }
}
