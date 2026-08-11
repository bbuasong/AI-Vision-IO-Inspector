using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EpsonScanApi.Services;

public static class Redactor
{
    private const int DefaultDpi = 300;

    /// <summary>
    /// rects: [[x, y, w, h], ...] 픽셀 좌표. DPI/해상도 보존, 무손실 PNG 저장.
    /// </summary>
    public static string CoverRegions(string inPath, string outPath, List<int[]> rects, string fill = "white")
    {
        using var im = Image.Load<Rgba32>(inPath);
        int dpi = im.Metadata.HorizontalResolution >= 150
            ? (int)im.Metadata.HorizontalResolution : DefaultDpi;

        var fillPixel = fill.ToLower() == "black"
            ? new Rgba32(0, 0, 0, 255)
            : new Rgba32(255, 255, 255, 255);

        im.ProcessPixelRows(acc =>
        {
            foreach (var r in rects ?? [])
            {
                if (r.Length < 4) continue;
                int rx = r[0], ry = r[1], rw = r[2], rh = r[3];
                for (int y = Math.Max(0, ry); y < Math.Min(acc.Height, ry + rh); y++)
                {
                    var row = acc.GetRowSpan(y);
                    int x0 = Math.Max(0, rx);
                    int x1 = Math.Min(row.Length, rx + rw);
                    for (int x = x0; x < x1; x++)
                        row[x] = fillPixel;
                }
            }
        });

        // 무손실 강제: JPEG 확장자여도 PNG로 저장
        if (outPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            outPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            outPath = Path.ChangeExtension(outPath, ".png");

        im.Metadata.HorizontalResolution = dpi;
        im.Metadata.VerticalResolution   = dpi;
        im.SaveAsPng(outPath);
        return outPath;
    }
}
