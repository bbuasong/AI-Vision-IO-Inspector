using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Ocr.Http
{
    public class HttpOcrApiTextReader : IOcrTextReader
    {
        private readonly Uri _endpoint;
        private readonly string _language;
        private readonly string _apiEngine;
        private readonly HttpClient _httpClient;

        public HttpOcrApiTextReader(string engineName, string environmentVariableName, string defaultBaseUrl)
            : this(engineName, environmentVariableName, defaultBaseUrl, "kor+eng", "auto")
        {
        }

        public HttpOcrApiTextReader(string engineName, string environmentVariableName, string defaultBaseUrl, string language, string apiEngine)
        {
            EngineName = string.IsNullOrWhiteSpace(engineName) ? "HTTP OCR API" : engineName;
            _endpoint = BuildEndpoint(ReadBaseUrl(environmentVariableName, defaultBaseUrl));
            _language = string.IsNullOrWhiteSpace(language) ? "kor+eng" : language;
            _apiEngine = string.IsNullOrWhiteSpace(apiEngine) ? "auto" : apiEngine;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
        }

        public string EngineName { get; private set; }

        public async Task<OcrTextReadResult> ReadAsync(string imageFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageFilePath))
                {
                    return OcrTextReadResult.CreateSuccess(EngineName, string.Empty);
                }

                OcrImageApiRequest request = new OcrImageApiRequest();
                request.ImagePath = imageFilePath;
                request.Language = _language;
                request.Engine = _apiEngine;

                string requestJson = SerializeRequest(request);
                using (StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = await _httpClient.PostAsync(_endpoint, content);
                    string responseText = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        return OcrTextReadResult.CreateFailure(EngineName, BuildFailureMessage(response, responseText));
                    }

                    OcrImageApiResponse responseModel = DeserializeResponse(responseText);
                    if (responseModel == null)
                    {
                        return OcrTextReadResult.CreateFailure(EngineName, "OCR API response JSON could not be parsed.");
                    }

                    string text = responseModel.Text;
                    string code = responseModel.PartNo;
                    string remoteEngine = responseModel.Engine;

                    OcrTextReadResult result = OcrTextReadResult.CreateSuccess(EngineName, text);
                    result.ExtractedCode = code;
                    result.Diagnostics = "Endpoint=" + _endpoint + "; RemoteEngine=" + remoteEngine;
                    return result;
                }
            }
            catch (Exception ex)
            {
                return OcrTextReadResult.CreateFailure(EngineName, ex.Message);
            }
        }

        private static string ReadBaseUrl(string environmentVariableName, string defaultBaseUrl)
        {
            string value = string.IsNullOrWhiteSpace(environmentVariableName)
                ? string.Empty
                : Environment.GetEnvironmentVariable(environmentVariableName);

            if (string.IsNullOrWhiteSpace(value))
            {
                value = defaultBaseUrl;
            }

            return value;
        }

        private static Uri BuildEndpoint(string baseUrl)
        {
            string value = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:8000" : baseUrl.Trim();
            if (value.EndsWith("/ocr-image", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(value);
            }

            return new Uri(value.TrimEnd('/') + "/ocr-image");
        }

        private static string BuildFailureMessage(HttpResponseMessage response, string responseText)
        {
            string body = string.IsNullOrWhiteSpace(responseText) ? string.Empty : responseText.Trim();
            if (body.Length > 500)
            {
                body = body.Substring(0, 500);
            }

            return "HTTP " + ((int)response.StatusCode).ToString() + " " + response.ReasonPhrase + " " + body;
        }

        private static string SerializeRequest(OcrImageApiRequest request)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OcrImageApiRequest));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, request);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static OcrImageApiResponse DeserializeResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OcrImageApiResponse));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(responseText)))
            {
                return serializer.ReadObject(stream) as OcrImageApiResponse;
            }
        }
    }

    [DataContract]
    public class OcrImageApiRequest
    {
        [DataMember(Name = "image_path")]
        public string ImagePath { get; set; }

        [DataMember(Name = "lang")]
        public string Language { get; set; }

        [DataMember(Name = "engine")]
        public string Engine { get; set; }
    }

    [DataContract]
    public class OcrImageApiResponse
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "part_no")]
        public string PartNo { get; set; }

        [DataMember(Name = "engine")]
        public string Engine { get; set; }
    }
}
