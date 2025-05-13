using System;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     "main.crdt" file content
    /// </summary>
    public readonly struct StaticSceneMessages
    {
        public static readonly StaticSceneMessages EMPTY = new (ReadOnlyMemory<byte>.Empty);

        public readonly ReadOnlyMemory<byte> Data;

        public StaticSceneMessages(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }
    }
}
