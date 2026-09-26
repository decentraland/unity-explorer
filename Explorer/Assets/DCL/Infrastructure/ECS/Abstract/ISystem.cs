using System;

namespace ECS.Abstract
{
    /// <summary>
    ///     Simple system without arguments and with a Single Update
    /// </summary>
    public interface ISystem : IDisposable
    {
        void Initialize();

        void Update();
    }
}
