#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.ApplicationMinimumSpecsGuard
{
    /// <summary>
    ///     Add this component to a GameObject with a MinimumSpecsTableView to stress test the UI.
    ///     Use context menu options to populate the table with random data or reset it with real data.
    /// </summary>
    [RequireComponent(typeof(MinimumSpecsTableView))]
    public class MinimumSpecsUIStressTester : MonoBehaviour
    {
        private MinimumSpecsTablePresenter tablePresenter;

        private void Awake()
        {
            var tableView = GetComponent<MinimumSpecsTableView>();
            tablePresenter = new MinimumSpecsTablePresenter(tableView);
        }

        [ContextMenu("Populate with Random Data")]
        private void PopulateWithRandomData()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var randomResults = new List<SpecResult>();
            var categories = (SpecCategory[])Enum.GetValues(typeof(SpecCategory));

            foreach (var category in categories)
            {
                randomResults.Add(new SpecResult(
                    category,
                    Random.value > 0.5f,
                    GetRandomWords(2, 4),
                    GetRandomWords(2, 3)
                ));
            }

            tablePresenter.Populate(randomResults);
        }

        [ContextMenu("Reset with Real Data")]
        private void ResetWithRealData()
        {
            if (!Application.isPlaying) return;

            var realGuard = new MinimumSpecsGuard(new DefaultSpecProfileProvider());
            realGuard.HasMinimumSpecs();
            tablePresenter.Populate(realGuard.Results);
        }

        private string GetRandomWords(int min, int max)
        {
            string[] words =
            {
                "mainframe override", "quantum entanglement", "neural uplink", "singularity breach", "subroutine anomaly", "data corruption detected", "synthetic consciousness", "asymmetric decryption", "logic bomb", "protocol handshake", "zero-day exploit", "sentient algorithm", "recursive paradox", "cybernetic uprising", "holographic firewall", "nanite swarm", "deep learning loop", "predictive heuristics", "entropy cascade", "encrypted payload", "autonomous directive", "sandbox escape", "self-replicating code", "malware genesis", "simulation collapse", "hyperthreaded cognition"
            };

            var sb = new StringBuilder();
            int count = Random.Range(min, max + 1);

            for (int i = 0; i < count; i++)
                sb.Append(words[Random.Range(0, words.Length)]).Append(" ");

            return sb.ToString().Trim();
        }
    }
}
#endif