using System;

namespace DCL.Diagnostics
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
