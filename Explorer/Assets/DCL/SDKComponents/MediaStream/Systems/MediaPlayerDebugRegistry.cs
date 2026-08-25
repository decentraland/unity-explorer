using System.Collections.Generic;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Hand-off point between the "Media Player" debug widget and the per-scene
    ///     <see cref="GatherMediaStreamDebugSystem" />: the widget requests a collection,
    ///     the current scene's system publishes the snapshot through <see cref="Update" />.
    ///     Both sides run on the main thread (scene worlds tick on the main thread),
    ///     so no locking.
    /// </summary>
    public class MediaPlayerDebugRegistry
    {
        private readonly List<(string name, string value)> rows = new ();

        /// <summary>
        ///     Per-player display rows of the last published snapshot, ready for the
        ///     debug panel's list element.
        /// </summary>
        public IReadOnlyList<(string name, string value)> Rows => rows;

        public bool CollectRequested { get; private set; }

        public int VideoPlayerCount { get; private set; }
        public int AudioStreamCount { get; private set; }
        public string SceneLabel { get; private set; } = string.Empty;

        /// <summary>
        ///     <see cref="UnityEngine.Time.frameCount" /> of the last published snapshot;
        ///     -1 until the first one. Lets the widget tell fresh data from leftovers of a
        ///     previous scene.
        /// </summary>
        public int LastCollectedFrame { get; private set; } = -1;

        public void RequestCollect() =>
            CollectRequested = true;

        /// <summary>
        ///     Publishes one completed collection and clears the pending request.
        ///     <paramref name="rows" /> is copied; the caller keeps ownership of its buffer.
        /// </summary>
        public void Update(string sceneLabel, int videoPlayerCount, int audioStreamCount, List<(string name, string value)> rows, int frame)
        {
            SceneLabel = sceneLabel;
            VideoPlayerCount = videoPlayerCount;
            AudioStreamCount = audioStreamCount;

            this.rows.Clear();
            this.rows.AddRange(rows);

            CollectRequested = false;
            LastCollectedFrame = frame;
        }
    }
}
