using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPatchArguments
    {
        public readonly List<IMultipartFormSection>? MultipartFormSections;
        public readonly string PatchData;
        public readonly string? ContentType;

        private GenericPatchArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            PatchData = string.Empty;
            ContentType = null;
        }

        private GenericPatchArguments(string patchData, string contentType)
        {
            MultipartFormSections = null;
            PatchData = patchData;
            ContentType = contentType;
        }

        public static GenericPatchArguments Create(string patchData, string contentType) =>
            new (patchData, contentType);

        public static GenericPatchArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericPatchArguments CreateJson(string patchData) =>
            new (patchData, "application/json");
    }
}
