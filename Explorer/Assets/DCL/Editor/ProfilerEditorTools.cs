using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace DCL.Editor
{
    public class ProfilerEditorTools
    {
        [MenuItem("🛠️ DCL/Log All Profiler Markers")]
        private static void EnumerateProfilerStatsMenuItem()
        {
            EnumerateProfilerStats();
        }

        private static void EnumerateProfilerStats()
        {
            var availableStatHandles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(availableStatHandles);

            var availableStats = new List<StatInfo>(availableStatHandles.Count);

            availableStats.AddRange(availableStatHandles
                                   .Select(ProfilerRecorderHandle.GetDescription)
                                   .Select(statDesc => new StatInfo
                                    {
                                        Cat = statDesc.Category,
                                        Name = statDesc.Name,
                                        Unit = statDesc.UnitType,
                                    }));

            availableStats.Sort((a, b) =>
            {
                int result = string.CompareOrdinal(a.Cat.ToString(), b.Cat.ToString());
                return result != 0 ? result : string.CompareOrdinal(a.Name, b.Name);
            });

            string filePath = Path.Combine(Application.dataPath, "AllProfilerMarkers.txt");

            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Available stats:");

                foreach (StatInfo s in availableStats)
                    writer.WriteLine($"{s.Cat}\t\t - {s.Name}\t\t - {s.Unit}");
            }

            Debug.Log($"Profiler stats saved to: {filePath}");
            AssetDatabase.Refresh();
        }

        private struct StatInfo
        {
            public ProfilerCategory Cat;
            public string Name;
            public ProfilerMarkerDataUnit Unit;
        }
    }
}
