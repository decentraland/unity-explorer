using System.Collections.Generic;
using DCL.Diagnostics;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTablePresenter
    {
        private const string PASS_ICON_SPRITE_TAG = "<sprite name=\"2705\">";
        private const string FAIL_ICON_SPRITE_TAG = "<sprite name=\"274c\">";
        
        private readonly Dictionary<SpecCategory, MinimumSpecsRowView> rowMap;

        public MinimumSpecsTablePresenter(MinimumSpecsTableView view)
        {
            rowMap = new Dictionary<SpecCategory, MinimumSpecsRowView>();

            foreach (var row in view.Rows)
            {
                if (rowMap.ContainsKey(row.Category))
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Duplicate SpecCategory in table: {row.Category}");

                rowMap[row.Category] = row;
            }
        }

        public void Populate(IEnumerable<SpecResult> results)
        {
            foreach (var result in results)
            {
                if (rowMap.TryGetValue(result.Category, out var row))
                {
                    string icon = result.IsMet ? PASS_ICON_SPRITE_TAG : FAIL_ICON_SPRITE_TAG;
                    string formattedActualText = $"{icon} {result.Actual}";

                    row.SetTitle(result.Category.ToString());
                    row.SetRequiredText(result.Required);
                    row.SetActualText(formattedActualText);
                }
                else
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"No UI row defined for category: {result.Category}");
            }
        }
    }
}