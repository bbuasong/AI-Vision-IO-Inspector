using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EpsonScanApi.Services;

/// <summary>
/// 라벨 자동 추출: projection-variance deskew + 잉크 bounding-box 크롭.
/// PIL card_pil.py의 C# 포트. OpenCV 불필요.
/// </summary>
public static class CardExtractor
{
    private const int DefaultDpi = 300;

    public static (string OutPath, Dictionary<string, object> Info)
        ExtractLabel(string inPath, string outPath, int dpi = 0, double marginFrac = 0.04, int cap = 150)
    {
        using var im = Image.Load<Rgba32>(inPath);
        int srcDpi = im.Metadata.HorizontalResolution >= 150
            ? (int)im.Metadata.HorizontalResolution : 0;
        int useDpi = dpi > 0 ? dpi : (srcDpi > 0 ? srcDpi : DefaultDpi);

        using var gray = im.CloneAs<L8>();
        double angle   = DeskewAngle(gray, cap);

        // 회전 (PIL 호환: 양수 = 반시계. ImageSharp 양수 = 시계 → 부호 반전)
        using var desk      = im.Clone(ctx => ctx.Rotate(-(float)angle).BackgroundColor(Color.White));
        using var grayDesk  = gray.CloneAs<Rgba32>().Clone(ctx => ctx.Rotate(-(float)angle).BackgroundColor(Color.White)).CloneAs<L8>();

        var bbox = InkOpenBbox(grayDesk, cap);
        bool cropped = false;

        if (bbox.HasValue)
        {
            var (x0, y0, x1, y1) = bbox.Value;
            int bw = x1 - x0, bh = y1 - y0;
            long fullArea = (long)desk.Width * desk.Height;
            if (bw > 0 && bh > 0 && (long)bw * bh < fullArea * 95 / 100)
            {
                int mx = (int)(bw * marginFrac), my = (int)(bh * marginFrac);
                int cx0 = Math.Max(0, x0 - mx), cy0 = Math.Max(0, y0 - my);
                int cx1 = Math.Min(desk.Width,  x1 + mx);
                int cy1 = Math.Min(desk.Height, y1 + my);
                desk.Mutate(ctx => ctx.Crop(new Rectangle(cx0, cy0, cx1 - cx0, cy1 - cy0)));
                cropped = true;
            }
        }

        // 항상 PNG
        outPath = Path.ChangeExtension(outPath, ".png");
        desk.Metadata.HorizontalResolution = useDpi;
        desk.Metadata.VerticalResolution   = useDpi;
        desk.SaveAsPng(outPath);

        var info = new Dictionary<string, object>
        {
            ["angle"]   = Math.Round(angle, 2),
            ["cropped"] = cropped,
            ["size"]    = new[] { desk.Width, desk.Height },
            ["dpi"]     = useDpi,
        };
        return (outPath, info);
    }

    // ── deskew: projection-variance (PIL card_pil._deskew_angle 포트) ────────
    private static double DeskewAngle(Image<L8> gray, int cap = 150,
                                      int dim = 1100, double rng = 20, double coarse = 1.0, double fine = 0.2)
    {
        using var small = gray.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(dim, dim), Mode = ResizeMode.Max,
        }));
        using var mask = InkOpen(small, cap);
        if (mask.GetBounds() == Rectangle.Empty) return 0.0;

        double best = 0, bestS = -1;
        for (double a = -rng; a <= rng; a += coarse)
        {
            double v = ProjScore(mask, a);
            if (v > bestS) { bestS = v; best = a; }
        }
        double fine0 = best - coarse;
        for (double a = fine0; a <= best + coarse; a += fine)
        {
            double v = ProjScore(mask, a);
            if (v > bestS) { bestS = v; best = a; }
        }
        return best;
    }

    // 잉크 bounding box (open 모폴로지 후)
    private static (int x0, int y0, int x1, int y1)? InkOpenBbox(Image<L8> gray, int cap)
    {
        using var m = InkOpen(gray, cap);
        var bb = m.GetBounds();
        if (bb == Rectangle.Empty) return null;
        return (bb.Left, bb.Top, bb.Right, bb.Bottom);
    }

    // 잉크 마스크 + open (MinFilter 3x3 → MaxFilter 3x3)
    private static Image<L8> InkOpen(Image<L8> gray, int cap)
    {
        using var ink = Ink(gray, cap);
        var opened = ink.Clone();
        MinFilter3x3(opened);
        MaxFilter3x3(opened);
        return opened;
    }

    // 잉크 마스크: Otsu(cap 제한) 이하 픽셀 = 255, 나머지 = 0
    private static Image<L8> Ink(Image<L8> gray, int cap)
    {
        byte thr = (byte)Math.Min(Preprocessor.OtsuThreshold(gray), cap);
        var result = new Image<L8>(gray.Width, gray.Height);
        result.ProcessPixelRows(gray, (rAcc, gAcc) =>
        {
            for (int y = 0; y < gAcc.Height; y++)
            {
                var src = gAcc.GetRowSpan(y);
                var dst = rAcc.GetRowSpan(y);
                for (int x = 0; x < src.Length; x++)
                    dst[x] = new L8(src[x].PackedValue < thr ? (byte)255 : (byte)0);
            }
        });
        return result;
    }

    // 3x3 최솟값 필터 (침식)
    private static void MinFilter3x3(Image<L8> img)
    {
        int w = img.Width, h = img.Height;
        var buf = new byte[w * h];
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte mn = 255;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Math.Clamp(x + dx, 0, w - 1);
                        int ny = Math.Clamp(y + dy, 0, h - 1);
                        byte v = acc.GetRowSpan(ny)[nx].PackedValue;
                        if (v < mn) mn = v;
                    }
                    buf[y * w + x] = mn;
                }
            }
        });
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < w; x++) row[x] = new L8(buf[y * w + x]);
            }
        });
    }

    // 3x3 최댓값 필터 (팽창)
    private static void MaxFilter3x3(Image<L8> img)
    {
        int w = img.Width, h = img.Height;
        var buf = new byte[w * h];
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte mx = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Math.Clamp(x + dx, 0, w - 1);
                        int ny = Math.Clamp(y + dy, 0, h - 1);
                        byte v = acc.GetRowSpan(ny)[nx].PackedValue;
                        if (v > mx) mx = v;
                    }
                    buf[y * w + x] = mx;
                }
            }
        });
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < w; x++) row[x] = new L8(buf[y * w + x]);
            }
        });
    }

    // 투영 분산 점수: 각도 a(PIL 호환 반시계)로 회전 후 행별 잉크 픽셀 수의 분산
    private static double ProjScore(Image<L8> mask, double angleDeg)
    {
        using var rotColor = mask.CloneAs<Rgba32>().Clone(ctx =>
            ctx.Rotate(-(float)angleDeg).BackgroundColor(Color.Black));
        using var rot = rotColor.CloneAs<L8>();
        int step  = Math.Max(1, rot.Height / 500);
        int xstep = Math.Max(1, rot.Width  / 500);
        var rows = new List<double>();
        rot.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y += step)
            {
                var row = acc.GetRowSpan(y);
                int cnt = 0;
                for (int x = 0; x < row.Length; x += xstep)
                    if (row[x].PackedValue > 0) cnt++;
                rows.Add(cnt);
            }
        });
        if (rows.Count == 0) return 0;
        double mean = rows.Average();
        return rows.Sum(r => (r - mean) * (r - mean));
    }
}

// ImageSharp 확장: GetBounds() — 비영(non-zero) 픽셀의 bounding box
internal static class ImageExtensions
{
    public static Rectangle GetBounds(this Image<L8> img)
    {
        int minX = img.Width, maxX = -1, minY = img.Height, maxY = -1;
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].PackedValue == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        });
        if (maxX < 0) return Rectangle.Empty;
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
