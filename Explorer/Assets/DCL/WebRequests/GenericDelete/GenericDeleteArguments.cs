using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests.GenericDelete
{
    public readonly struct GenericDeleteArguments
    {
        public readonly List<IMultipartFormSection>? MultipartFormSections;
        public readonly string DeleteData;
        public readonly string? ContentType;

        private GenericDeleteArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            DeleteData = string.Empty;
            ContentType = null;
        }

        private GenericDeleteArguments(string deleteData, string contentType)
        {
            MultipartFormSections = null;
            DeleteData = deleteData;
            ContentType = contentType;
        }

        public static GenericDeleteArguments Empty => new (string.Empty, "application/json");

        public static GenericDeleteArguments FromMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericDeleteArguments Create(string deleteData, string contentType) =>
            new (deleteData, contentType);

        public static GenericDeleteArguments FromJson(string deleteData) =>
            new (deleteData, "application/json");

        public static GenericDeleteArguments FromJsonOrDefault(string? deleteData) =>
            deleteData == null ? Empty : FromJson(deleteData);

        public override string ToString() =>
            "GenericDeleteArguments:"
            + $"\nMultipartFormSections: {MultipartFormSections}"
            + $"\ndeleteData: {DeleteData}"
            + $"\nContentType: {ContentType}";
    }
}
