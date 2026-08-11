using System.Text.Json.Serialization;

namespace EpsonScanApi.Models;

public class JobModel
{
    [JsonPropertyName("id")]             public string Id { get; set; } = "";
    [JsonPropertyName("status")]         public string Status { get; set; } = "created";
    [JsonPropertyName("created_at")]     public string CreatedAt { get; set; } = "";
    [JsonPropertyName("updated_at")]     public string UpdatedAt { get; set; } = "";
    [JsonPropertyName("image_path")]     public string? ImagePath { get; set; }
    [JsonPropertyName("processed_path")] public string? ProcessedPath { get; set; }
    [JsonPropertyName("card_path")]      public string? CardPath { get; set; }
    [JsonPropertyName("redacted_path")]  public string? RedactedPath { get; set; }
    [JsonPropertyName("pdf_path")]       public string? PdfPath { get; set; }
    [JsonPropertyName("ocr_src_path")]   public string? OcrSrcPath { get; set; }
    [JsonPropertyName("error")]          public string? Error { get; set; }
    [JsonPropertyName("card_log")]       public string? CardLog { get; set; }
    [JsonPropertyName("ocr")]            public object? Ocr { get; set; }
    [JsonPropertyName("params")]         public object? Params { get; set; }
}
