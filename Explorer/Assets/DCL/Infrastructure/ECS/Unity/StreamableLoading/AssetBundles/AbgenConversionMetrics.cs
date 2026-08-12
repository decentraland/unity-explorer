#nullable enable

using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Live record of the session's abgen conversions, written from the sidecar's warm-up flow and read
    ///     by the scene dev console's "AB Conversion" panel on the main thread: per-file entries and the
    ///     whole-scene warm-up stage of the eager build. A process-wide instance: the writers are static or
    ///     singular and the reader lives in the global UI, so there is exactly one pipeline to describe.
    /// </summary>
    public sealed class AbgenConversionMetrics
    {
        public enum ConversionStatus
        {
            Converting,
            Converted,

            /// <summary>Completed by the sidecar server; the per-file outcome is only visible as the manifest's exitCode.</summary>
            Processed,
            Failed,
            Cancelled,

            /// <summary>Not a file: a warm-up milestone rendered as an informational row (Path carries the message).</summary>
            Milestone,
        }

        public enum WarmUpStage
        {
            Inactive,
            Converting,
            Ready,
            Failed,
        }

        public static readonly AbgenConversionMetrics INSTANCE = new ();

        private readonly object gate = new ();
        private readonly Dictionary<string, Entry> entries = new ();

        private int planned;
        private int succeeded;
        private int failed;
        private int inFlight;
        private long totalOutputBytes;
        private int sequence;
        private int version;

        private WarmUpStage warmUpStage;
        private string? warmUpSceneId;
        private float warmUpElapsedSeconds;
        private bool warmUpAlreadyWarm;

        /// <summary>Bumped on every change so readers can skip rebuilding an unchanged view.</summary>
        public int Version
        {
            get
            {
                lock (gate) { return version; }
            }
        }

        public int Planned
        {
            get
            {
                lock (gate) { return planned; }
            }
        }

        public int Succeeded
        {
            get
            {
                lock (gate) { return succeeded; }
            }
        }

        public int Failed
        {
            get
            {
                lock (gate) { return failed; }
            }
        }

        public int InFlight
        {
            get
            {
                lock (gate) { return inFlight; }
            }
        }

        public long TotalOutputBytes
        {
            get
            {
                lock (gate) { return totalOutputBytes; }
            }
        }

        public WarmUpStage WarmUp
        {
            get
            {
                lock (gate) { return warmUpStage; }
            }
        }

        public string? WarmUpSceneId
        {
            get
            {
                lock (gate) { return warmUpSceneId; }
            }
        }

        public float WarmUpElapsedSeconds
        {
            get
            {
                lock (gate) { return warmUpElapsedSeconds; }
            }
        }

        /// <summary>True when the ready manifest came from an already-converted corpus (no build ran).</summary>
        public bool WarmUpAlreadyWarm
        {
            get
            {
                lock (gate) { return warmUpAlreadyWarm; }
            }
        }

        public void OnWarmUpStarted(string sceneEntityId)
        {
            lock (gate)
            {
                warmUpStage = WarmUpStage.Converting;
                warmUpSceneId = sceneEntityId;
                warmUpElapsedSeconds = 0;
                warmUpAlreadyWarm = false;
                version++;
            }
        }

        public void OnWarmUpReady(float elapsedSeconds, bool alreadyWarm)
        {
            lock (gate)
            {
                warmUpStage = WarmUpStage.Ready;
                warmUpElapsedSeconds = elapsedSeconds;
                warmUpAlreadyWarm = alreadyWarm;
                version++;
            }
        }

        public void OnWarmUpFailed()
        {
            lock (gate)
            {
                warmUpStage = WarmUpStage.Failed;
                version++;
            }
        }

        /// <summary>A new warmup owns the session: previous scene's (or pre-reload) history is dropped.</summary>
        public void Reset()
        {
            lock (gate)
            {
                entries.Clear();
                planned = 0;
                succeeded = 0;
                failed = 0;
                inFlight = 0;
                totalOutputBytes = 0;
                sequence = 0;
                version++;
            }
        }

        public void OnPlanned(int count)
        {
            lock (gate)
            {
                planned = count;
                version++;
            }
        }

        public void OnStarted(string contentPath)
        {
            lock (gate)
            {
                entries[contentPath] = new Entry
                {
                    Path = contentPath,
                    Status = ConversionStatus.Converting,
                    Sequence = sequence++,
                };

                inFlight++;
                version++;
            }
        }

        public void OnSucceeded(string contentPath, string artifactName, int outputBytes, long elapsedMs)
        {
            lock (gate)
            {
                Entry entry = TakeEntry(contentPath);
                entry.Status = ConversionStatus.Converted;
                entry.ArtifactName = artifactName;
                entry.OutputBytes = outputBytes;
                entry.ElapsedMs = elapsedMs;
                entries[contentPath] = entry;

                inFlight--;
                succeeded++;
                totalOutputBytes += outputBytes;
                version++;
            }
        }

        /// <summary>Adds an informational row to the panel without touching the counters.</summary>
        public void OnMilestone(string message)
        {
            lock (gate)
            {
                entries[message] = new Entry
                {
                    Path = message,
                    Status = ConversionStatus.Milestone,
                    Sequence = sequence++,
                };

                version++;
            }
        }

        /// <summary>Sidecar warm-up file completion: no bytes/elapsed detail crosses the progress endpoint.</summary>
        public void OnProcessed(string contentPath)
        {
            lock (gate)
            {
                Entry entry = TakeEntry(contentPath);
                entry.Status = ConversionStatus.Processed;
                entries[contentPath] = entry;

                inFlight--;
                succeeded++;
                version++;
            }
        }

        public void OnFailed(string contentPath, string error)
        {
            lock (gate)
            {
                Entry entry = TakeEntry(contentPath);
                entry.Status = ConversionStatus.Failed;
                entry.Error = error;
                entries[contentPath] = entry;

                inFlight--;
                failed++;
                version++;
            }
        }

        public void OnCancelled(string contentPath)
        {
            lock (gate)
            {
                Entry entry = TakeEntry(contentPath);
                entry.Status = ConversionStatus.Cancelled;
                entries[contentPath] = entry;

                inFlight--;
                version++;
            }
        }

        /// <summary>
        ///     Fills <paramref name="target" /> with the current entries in chronological order (oldest
        ///     first, console-style). An entry keeps its original position when its status changes later.
        /// </summary>
        public void CopySnapshot(List<Entry> target)
        {
            target.Clear();

            lock (gate)
            {
                foreach (Entry entry in entries.Values)
                    target.Add(entry);
            }

            target.Sort(static (a, b) => a.Sequence.CompareTo(b.Sequence));
        }

        private Entry TakeEntry(string contentPath) =>
            entries.TryGetValue(contentPath, out Entry entry) ? entry : new Entry { Path = contentPath, Sequence = sequence++ };

        public struct Entry
        {
            public string Path;
            public ConversionStatus Status;
            public string? ArtifactName;
            public string? Error;
            public int OutputBytes;
            public long ElapsedMs;
            public int Sequence;
        }
    }
}
