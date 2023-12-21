using System;

namespace DCL.Utilities.Extensions
{
    public static class NullExtensions
    {
        public static T EnsureNotNull<T>(this T? value, string? message = null)
        {
            if (value == null)
                throw new NullReferenceException(message ?? $"Value of type {typeof(T).FullName} is null");

            return value;
        }
    }
}
