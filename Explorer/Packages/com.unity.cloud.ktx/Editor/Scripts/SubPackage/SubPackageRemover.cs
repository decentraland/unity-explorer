// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SubPackage
{
    static class SubPackageRemover
    {
        const string k_PackageName = "KTX for Unity";
        const string k_DialogTitle = "Removing Obsolete Packages";
        const string k_CleanupRegex = @"^com\.unity\.cloud\.ktx\.webgl-.*$";

        static readonly string k_DialogText = $"Obsolete packages where found! Those were needed by previous versions of, and are now in conflict with {k_PackageName}. They will be removed now.";
        static readonly string k_ErrorMessage = $"Error removing {k_PackageName} WebGL sub-packages.";

        [InitializeOnLoadMethod]
        static async Task TryRemoveObsoleteSubPackagesAsync()
        {
#if UNITY_2020
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_SUB_PACKAGE_LOAD")))
            {
                Debug.Log($"{k_PackageName} WebGL sub-package removal: Skipped due to environment variable DISABLE_SUB_PACKAGE_LOAD.");
                return;
            }
#endif
            try
            {
                var installedPackages = await GetAllInstalledPackagesAsync();
                var subPackages = GetSubPackages(installedPackages);

                if (subPackages.Count > 0)
                {
                    DisplayDialog();

                    var packagesToRemove = subPackages
                        .Select(p => p.name);

                    await RemovePackagesAsync(packagesToRemove);
                }
            }
            catch (Exception e)
            {
                //  Explicit logging is required to avoid silent failures due to this task
                //  being triggered as fire and forget.
                Debug.LogException(e);
            }
        }

        static async Task RemovePackagesAsync(IEnumerable<string> remove)
        {
#if UNITY_2021_2_OR_NEWER
            await AddAndRemoveAsync(remove.ToArray());
#else
            foreach (var package in remove)
            {
                await RemoveAsync(package);
            }
#endif
        }

#if UNITY_2021_2_OR_NEWER
        static async Task AddAndRemoveAsync(string[] remove)
        {
            var result = Client.AddAndRemove(new string[] {}, remove);

            while (!result.IsCompleted)
                await Yield();

            if (result.Status != StatusCode.Success)
                Debug.LogError(result.Error.message);
        }
#else

        static async Task RemoveAsync(string package, double timeout = 60)
        {
            var startTime = EditorApplication.timeSinceStartup;
            var result = Client.Remove(package);

            while (!result.IsCompleted && EditorApplication.timeSinceStartup - startTime <= timeout)
            {
                await Yield();
            }

            if (!result.IsCompleted || result.Status != StatusCode.Success)
            {
                Debug.LogError(k_ErrorMessage);

                if (result.Status != StatusCode.Success)
                {
                    Debug.LogError(result.Error.message);
                }
            }
        }
#endif

        static async Task<List<PackageInfo>> GetAllInstalledPackagesAsync(double timeout = 60)
        {
            var startTime = EditorApplication.timeSinceStartup;
            var request = Client.List(offlineMode: true, includeIndirectDependencies: false);

            while (!request.IsCompleted && EditorApplication.timeSinceStartup - startTime <= timeout)
            {
                await Yield();
            }

            if (!request.IsCompleted)
            {
                throw new TimeoutException(k_ErrorMessage);
            }
            Assert.AreEqual(StatusCode.Success, request.Status, $"{k_ErrorMessage}. Failed fetching installed packages.");

            return request.Result.ToList();
        }

        static List<PackageInfo> GetSubPackages(IEnumerable<PackageInfo> installedPackages)
        {
            var regex = new Regex(k_CleanupRegex, RegexOptions.CultureInvariant, TimeSpan.FromMinutes(1));

            return installedPackages
                .Where(package => regex.IsMatch(package.name))
                .ToList();
        }

        static void DisplayDialog()
        {
            if (Application.isBatchMode)
                return;

            EditorUtility.DisplayDialog(k_DialogTitle, k_DialogText, "Ok");
        }

        static async Task Yield()
        {
            if (Application.isBatchMode)
                Thread.Sleep(10);
            else
                await Task.Yield();
        }
    }
}
