using System;

namespace DCL.Diagnostics
{
    public class EcsEntityNotFoundException : Exception
    {
        public readonly int EntityId;

        public EcsEntityNotFoundException(int entityId, string message) : base(message)
        {
            EntityId = entityId;
        }
    }
}
