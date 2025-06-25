using System.Collections.Generic;
using DCL.Diagnostics;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTablePresenter
    {
        private readonly MinimumSpecsTableView view;
        private readonly Dictionary<SpecCategory, MinimumSpecsRowView> rowMap;

        public MinimumSpecsTablePresenter(MinimumSpecsTableView view)
        {
            this.view = view;

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
                    row.Set(result);
                else
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"No UI row defined for category: {result.Category}");
            }
        }

        public void Clear()
        {
            foreach (var row in rowMap.Values)
                row.Clear();
        }
    }
}