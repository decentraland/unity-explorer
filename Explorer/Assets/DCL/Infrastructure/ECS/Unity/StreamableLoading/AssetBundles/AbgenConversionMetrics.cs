#nullable enable

using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Live record of the session's in-process abgen conversions, written from the conversion flow on
    ///     worker threads and read by the scene dev console's "AB Conversion" panel on the main thread.
    ///     A process-wide instance: the writer (<see cref="AbgenAssetBundleFallback" />) is static and the
    ///     reader lives in the global UI, so there is exactly one conversion pipeline to describe.
    /// </summary>
    public sealed class AbgenConversionMetrics
    {
        public enum ConversionStatus
        {
            Converting,
            Converted,
            Failed,
            Cancelled,
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

        /// <summary>Fills <paramref name="target" /> with the current entries, most recently started first.</summary>
        public void CopySnapshot(List<Entry> target)
        {
            target.Clear();

            lock (gate)
            {
                foreach (Entry entry in entries.Values)
                    target.Add(entry);
            }

            target.Sort(static (a, b) => b.Sequence.CompareTo(a.Sequence));
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
