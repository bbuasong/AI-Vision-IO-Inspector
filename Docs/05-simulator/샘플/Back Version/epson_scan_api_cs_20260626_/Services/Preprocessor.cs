using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using EpsonScanApi.Models;

namespace EpsonScanApi.Services;

public static class Preprocessor
{
    public static void ProcessFile(string inPath, string outPath, PreprocessRequest opts)
    {
        using var im = Image.Load<Rgba32>(inPath);

        if (opts.BorderCrop > 0)
        {
            int n = opts.BorderCrop;
            if (im.Width > 2 * n && im.Height > 2 * n)
                im.Mutate(x => x.Crop(new Rectangle(n, n, im.Width - 2 * n, im.Height - 2 * n)));
        }

        if (opts.Rotate != 0)
            // PIL: im.rotate(-rotate, expand=True, fillcolor="white") = rotate CW by `rotate`.
            // ImageSharp Rotate(positive) is already CW, so use the value as-is (do NOT negate).
            // PIL's default rotate resample is NEAREST, and exposed corners are filled white.
            im.Mutate(x => x.Rotate(opts.Rotate, KnownResamplers.NearestNeighbor).BackgroundColor(Color.White));

        bool needGray = opts.Grayscale || opts.Binarize is not (null or "none") || opts.Deskew;
        // PIL Image.convert("L") uses ITU-R 601-2 (BT.601) luma; match it (ImageSharp defaults to BT.709).
        if (needGray) im.Mutate(x => x.Grayscale(GrayscaleMode.Bt601));

        if (opts.Denoise)
            im.Mutate(x => x.MedianBlur(1, true));

        if (opts.Autocontrast)
            ApplyAutocontrast(im);

        if (opts.Deskew)
        {
            using var graySnap = im.CloneAs<L8>();
            float ang = EstimateSkew(graySnap);
            if (Math.Abs(ang) >= 0.5f)
                im.Mutate(x => x.Rotate(ang).BackgroundColor(Color.White));
        }

        if (opts.Binarize is not (null or "none"))
        {
            using var g = im.CloneAs<L8>();
            byte thr = opts.Binarize == "fixed"
                ? (byte)Math.Clamp(opts.Threshold, 0, 255)
                : OtsuThreshold(g);
            using var bin = new Image<L8>(im.Width, im.Height);
            bin.ProcessPixelRows(g, (binAcc, gAcc) =>
            {
                for (int y = 0; y < gAcc.Height; y++)
                {
                    var src = gAcc.GetRowSpan(y);
                    var dst = binAcc.GetRowSpan(y);
                    for (int x2 = 0; x2 < src.Length; x2++)
                        dst[x2] = new L8(src[x2].PackedValue > thr ? byte.MaxValue : byte.MinValue);
                }
            });
            // Python applies resize_maxdim AFTER binarize. PIL forces NEAREST when resizing a 1-bit ("1") image.
            if (opts.ResizeMaxdim > 0)
            {
                int bm = opts.ResizeMaxdim;
                int bmx = Math.Max(bin.Width, bin.Height);
                if (bmx > bm)
                {
                    int bnw = Math.Max(1, (int)(bin.Width  * (double)bm / bmx));
                    int bnh = Math.Max(1, (int)(bin.Height * (double)bm / bmx));
                    bin.Mutate(x => x.Resize(bnw, bnh, KnownResamplers.NearestNeighbor));
                }
            }
            bin.SaveAsPng(outPath);
            return;
        }

        if (opts.ResizeMaxdim > 0)
        {
            int m = opts.ResizeMaxdim;
            int mx = Math.Max(im.Width, im.Height);
            if (mx > m)
            {
                int nw = Math.Max(1, (int)(im.Width  * (double)m / mx));
                int nh = Math.Max(1, (int)(im.Height * (double)m / mx));
                im.Mutate(x => x.Resize(nw, nh, KnownResamplers.Lanczos3));
            }
        }

        im.SaveAsPng(outPath);
    }

    private static float EstimateSkew(Image<L8> gray)
    {
        using var small = gray.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(800, 800), Mode = ResizeMode.Max,
        }));
        float best = 0f; double bestScore = -1;
        for (float a = -4f; a <= 4f; a += 0.5f)
        {
            using var rot = small.Clone(x => x.Rotate(a));
            double score = RowVarianceScore(rot);
            if (score > bestScore) { bestScore = score; best = a; }
        }
        return best;
    }

    private static double RowVarianceScore(Image<L8> img)
    {
        int step = Math.Max(1, img.Height / 200);
        int xstep = Math.Max(1, img.Width  / 200);
        var rows = new List<double>();
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y += step)
            {
                var row = acc.GetRowSpan(y);
                double s = 0;
                for (int x = 0; x < row.Length; x += xstep)
                    s += 255 - row[x].PackedValue;
                rows.Add(s);
            }
        });
        if (rows.Count == 0) return 0;
        double mean = rows.Average();
        return rows.Sum(r => (r - mean) * (r - mean));
    }

    // Faithful port of PIL ImageOps.autocontrast(im, cutoff=1):
    //   - per-band histogram (R/G/B independently)
    //   - remove cutoff% of pixels from each end (n*cutoff//100, integer floor)
    //   - lo/hi = first/last remaining non-empty bin
    //   - lut[ix] = int(ix*scale + offset) clamped to 0..255, scale=255/(hi-lo)
    private static void ApplyAutocontrast(Image<Rgba32> im, int cutoff = 1)
    {
        int[][] hist = { new int[256], new int[256], new int[256] };
        im.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
                foreach (ref Rgba32 px in acc.GetRowSpan(y))
                {
                    hist[0][px.R]++; hist[1][px.G]++; hist[2][px.B]++;
                }
        });

        var luts = new byte[3][];
        for (int band = 0; band < 3; band++)
            luts[band] = BuildAutocontrastLut(hist[band], cutoff);

        im.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    row[x] = new Rgba32(luts[0][row[x].R], luts[1][row[x].G], luts[2][row[x].B], row[x].A);
            }
        });
    }

    private static byte[] BuildAutocontrastLut(int[] hist, int cutoff)
    {
        // work on a copy so we can subtract the cutoff pixels
        int[] h = (int[])hist.Clone();
        int n = h.Sum();
        if (cutoff > 0 && n > 0)
        {
            int cut = n * cutoff / 100;             // low end
            for (int lo = 0; lo < 256 && cut > 0; lo++)
            {
                if (cut > h[lo]) { cut -= h[lo]; h[lo] = 0; }
                else { h[lo] -= cut; cut = 0; }
            }
            cut = n * cutoff / 100;                  // high end
            for (int hi = 255; hi >= 0 && cut > 0; hi--)
            {
                if (cut > h[hi]) { cut -= h[hi]; h[hi] = 0; }
                else { h[hi] -= cut; cut = 0; }
            }
        }

        int low = 0;  while (low < 256 && h[low] == 0) low++;
        int high = 255; while (high >= 0 && h[high] == 0) high--;

        var lut = new byte[256];
        if (high <= low)
        {
            // degenerate: identity map (matches PIL leaving the band unchanged)
            for (int i = 0; i < 256; i++) lut[i] = (byte)i;
            return lut;
        }

        double scale = 255.0 / (high - low);
        double offset = -low * scale;
        for (int i = 0; i < 256; i++)
        {
            int v = (int)(i * scale + offset);       // PIL uses int() truncation
            lut[i] = (byte)Math.Clamp(v, 0, 255);
        }
        return lut;
    }

    internal static byte OtsuThreshold(Image<L8> gray)
    {
        int[] hist = new int[256];
        gray.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
                foreach (ref L8 px in acc.GetRowSpan(y))
                    hist[px.PackedValue]++;
        });
        int total = hist.Sum();
        if (total == 0) return 127;
        long sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += i * hist[i];
        double sumB = 0; int wB = 0; double maxVar = -1; byte thr = 127;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            int wF = total - wB;
            if (wF == 0) break;
            sumB += t * hist[t];
            double mB = sumB / wB, mF = (sumAll - sumB) / wF;
            double v = wB * (double)wF * (mB - mF) * (mB - mF);
            if (v > maxVar) { maxVar = v; thr = (byte)t; }
        }
        return thr;
    }
}
