using DCL.Utilities.Extensions;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class SeveralRoomDisplay : IRoomDisplay
    {
        private readonly IReadOnlyList<IRoomDisplay> list;

        public SeveralRoomDisplay(params IRoomDisplay[] list) : this(list.AsReadOnly()) { }

        public SeveralRoomDisplay(IReadOnlyList<IRoomDisplay> list)
        {
            this.list = list;
        }

        public void Update()
        {
            for (var i = 0; i < list.Count; i++)
                list[i].Update();
        }
    }
}
