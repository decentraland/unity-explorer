using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Addressables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Validatables
{
    public interface IValidatableAsset
    {
        /// <summary>
        ///     Due ReportHub.LogWarning might not be initialized yet
        /// </summary>
        private static readonly Action<string> LOG_WARNING = m => ReportHub.Log(ReportData.UNSPECIFIED, m);

        async UniTask EnsureValidAsync()
        {
            var type = this.GetType();
            LOG_WARNING($"{type.FullName} doesn't support the direct validation, for the performance consider the usage of the direct implementation");

            var exception = await this.InvalidValuesAsync();

            if (exception != null)
                throw exception;
        }
    }

    public static class ValidateAssetsExtensions
    {
        public static async UniTask<AggregateException?> InvalidValuesAsync(this IValidatableAsset validatableAsset)
        {
            var invalidList = new List<Exception>();

            async UniTask CheckAsync((AssetReference reference, string name) value)
            {
                var exception = await value.reference!.EnsureValidWithExceptionAsync(value.name!);

                if (exception != null)
                    invalidList.Add(exception);
            }

            await UniTask.WhenAll(validatableAsset.ValuesForChecking().Select(CheckAsync)!);

            return invalidList.Any()
                ? new AggregateException("Some AssetReferences are not valid", invalidList)
                : null;
        }

        private static IReadOnlyList<(AssetReference reference, string name)> ValuesForChecking(this IValidatableAsset validatableAsset)
        {
            const BindingFlags BINDING = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = validatableAsset.GetType();
            var list = new List<(AssetReference reference, string name)>();
            var handled = new HashSet<AssetReference>();

            void TryAdd(AssetReference reference, string name)
            {
                if (handled.Contains(reference))
                    return;

                handled.Add(reference);
                list.Add((reference, name));
            }

            foreach (var property in type.GetProperties(BINDING))
                if (TryImplementsType<AssetReference>(validatableAsset, property, out var value))
                    TryAdd(value!, property.Name);

            foreach (var field in type.GetFields(BINDING))
                if (TryImplementsType<AssetReference>(validatableAsset, field, out var value))
                    TryAdd(value!, field.Name);

            return list;
        }

        private static bool TryImplementsType<T>(this IValidatableAsset validatableAsset, PropertyInfo property, out T? implementingValue)
        {
            var checkingType = property.PropertyType;

            if (checkingType == typeof(T)
                || checkingType.IsSubclassOf(typeof(T)))
            {
                implementingValue = (T)property.GetValue(validatableAsset);
                return true;
            }

            implementingValue = default(T);
            return false;
        }

        private static bool TryImplementsType<T>(this IValidatableAsset validatableAsset, FieldInfo property, out T? implementingValue)
        {
            var checkingType = property.FieldType;

            if (checkingType == typeof(T)
                || checkingType.IsSubclassOf(typeof(T)))
            {
                implementingValue = (T)property.GetValue(validatableAsset);
                return true;
            }

            implementingValue = default(T);
            return false;
        }
    }
}
