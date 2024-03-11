using System;
using UnityEngine;

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

        public static T EnsureGetComponent<T>(this GameObject? value) =>
            value.EnsureNotNull("GameObject is null")
                 .GetComponent<T>()
                 .EnsureNotNull($"{typeof(T).Name} component not found on the gameobject {value.name}");
    }
}
