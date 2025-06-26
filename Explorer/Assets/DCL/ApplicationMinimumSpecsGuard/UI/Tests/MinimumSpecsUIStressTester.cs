#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.ApplicationMinimumSpecsGuard.UI.Tests
{
    /// <summary>
    ///     An editor-only component for stress-testing the Minimum Specs UI at runtime.
    ///     Add this to the same GameObject as the MinimumSpecsTableView.
    ///     Use the context menu to trigger actions while the application is in Play Mode.
    /// </summary>
    [RequireComponent(typeof(MinimumSpecsTableView))]
    public class MinimumSpecsUIStressTester : MonoBehaviour
    {
        private MinimumSpecsTableView tableView;
        private MinimumSpecsTablePresenter tablePresenter;
        private MinimumSpecsGuard realSpecsGuard;

        private void Awake()
        {
            // This component should only function in the editor, so we disable it in a build.
            // The #if UNITY_EDITOR above will strip the whole file, but this is an extra layer of safety.
#if !UNITY_EDITOR
            gameObject.SetActive(false);
#endif
        }

        private void Start()
        {
            tableView = GetComponent<MinimumSpecsTableView>();
            tablePresenter = new MinimumSpecsTablePresenter(tableView);
        }

        [ContextMenu("Populate with Random Data")]
        private void PopulateWithRandomData()
        {
            if (!Application.isPlaying) return;

            Populate(CreateRandomResults());
        }

        [ContextMenu("Reset with Real Data")]
        private void ResetWithRealData()
        {
            if (!Application.isPlaying) return;

            if (realSpecsGuard == null)
            {
                realSpecsGuard = new MinimumSpecsGuard(new DefaultSpecProfileProvider());
                realSpecsGuard.HasMinimumSpecs();
            }

            Populate(realSpecsGuard.Results);
        }

        private void Populate(IEnumerable<SpecResult> results)
        {
            tablePresenter.Populate(results);
        }

        private List<SpecResult> CreateRandomResults()
        {
            var randomResults = new List<SpecResult>();
            foreach (SpecCategory category in Enum.GetValues(typeof(SpecCategory)))
            {
                var randomResult = new SpecResult(
                    category,
                    Random.Range(0, 2) == 0,
                    RandomDataUtils.GetRandomWords(3, 8),
                    RandomDataUtils.GetRandomWords(3, 8)
                );
                randomResults.Add(randomResult);
            }

            return randomResults;
        }
    }
}
#endif