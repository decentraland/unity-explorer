using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPutArguments
    {
        public readonly List<IMultipartFormSection> MultipartFormSections;
        public readonly string PutData;
        public readonly string ContentType;

        private GenericPutArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            PutData = string.Empty;
            ContentType = null;
        }

        private GenericPutArguments(string putData, string contentType)
        {
            MultipartFormSections = null;
            PutData = putData;
            ContentType = contentType;
        }

        public static GenericPutArguments Create(string putData, string contentType) =>
            new (putData, contentType);

        public static GenericPutArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericPutArguments CreateJson(string putData) =>
            new (putData, "application/json");
    }
}
