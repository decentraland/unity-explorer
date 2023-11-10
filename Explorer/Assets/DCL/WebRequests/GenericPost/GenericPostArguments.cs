using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostArguments
    {
        public readonly List<IMultipartFormSection> MultipartFormSections;
        public readonly string PostData;
        public readonly string ContentType;

        private GenericPostArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            PostData = string.Empty;
            ContentType = null;
        }

        private GenericPostArguments(string postData, string contentType)
        {
            MultipartFormSections = null;
            PostData = postData;
            ContentType = contentType;
        }

        public static GenericPostArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericPostArguments CreateJson(string postData) =>
            new (postData, "application/json");
    }
}
