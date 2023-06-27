using System;

namespace Diagnostics.ReportsHandling
{
    [AttributeUsage(AttributeTargets.Class)]
    public class LogCategoryAttribute : Attribute
    {
        public readonly string Category;

        public LogCategoryAttribute(string category)
        {
            Category = category;
        }
    }
}
