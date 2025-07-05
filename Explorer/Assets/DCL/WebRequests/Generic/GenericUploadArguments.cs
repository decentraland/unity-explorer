using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericUploadArguments
    {
        public readonly List<IMultipartFormSection>? MultipartFormSections;
        public readonly WWWForm? WWWForm;
        public readonly string? PostData;
        public readonly string? ContentType;

        private GenericUploadArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            PostData = string.Empty;
            ContentType = null;
            WWWForm = null;
        }

        private GenericUploadArguments(string postData, string contentType)
        {
            MultipartFormSections = null;
            PostData = postData;
            ContentType = contentType;
            WWWForm = null;
        }

        private GenericUploadArguments(WWWForm form)
        {
            MultipartFormSections = null;
            PostData = string.Empty;
            ContentType = null;
            WWWForm = form;
        }

        public static GenericUploadArguments Empty => new (string.Empty, "application/json");

        public static GenericUploadArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericUploadArguments CreateWWWForm(WWWForm form) =>
            new (form);

        public static GenericUploadArguments Create(string postData, string contentType) =>
            new (postData, contentType);

        public static GenericUploadArguments CreateJson(string postData) =>
            new (postData, "application/json");

        public static GenericUploadArguments CreateJsonOrDefault(string? postData) =>
            postData == null ? Empty : CreateJson(postData);

        public override string ToString() =>
            "GenericUploadArguments:"
            + $"\nMultipartFormSections: {MultipartFormSections}"
            + $"\nWWWForm: {WebFormToString(WWWForm)}"
            + $"\nPostData: {PostData}"
            + $"\nContentType: {ContentType}";

        private static readonly IReadOnlyDictionary<string, string> EMPTY_DICTIONARY = new Dictionary<string, string>();

        private static string WebFormToString(WWWForm? wwwForm)
        {
            if (wwwForm == null) return "Empty web form";

            var sb = new StringBuilder();

            foreach ((string? key, string? value) in wwwForm.headers ?? EMPTY_DICTIONARY)
                sb.Append($"{key} : {value} \t");

            return sb.ToString();
        }
    }
}
