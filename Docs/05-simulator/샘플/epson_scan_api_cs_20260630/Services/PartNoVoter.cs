using System.Text.RegularExpressions;

namespace EpsonScanApi.Services;

/// <summary>
/// 다중 스캔 다수결: 같은 라벨을 여러 번 스캔한 part_no 후보들을 합쳐 하나로 합의.
/// 스캔마다 위치/기울기가 달라 '서로 다른 곳에서' 틀리므로, 모으면 진짜 값이 드러난다.
///   1) 완전일치 최빈값이 단독 우세하면 그것.
///   2) 아니면 medoid(다른 후보들과 편집거리 합이 최소인 후보) 선택. 동점이면 품질 높은 것.
/// 신뢰도 = 합의와 거의 같은(편집거리<=1) 후보 비율. n<3 이거나 신뢰도<0.6 이면 검토필요.
/// </summary>
public static class PartNoVoter
{
    public record VoteResult(string PartNo, double Confidence, int N, bool NeedsReview);

    public static VoteResult Vote(IReadOnlyList<(string PartNo, double Quality)> reads)
    {
        var items = reads
            .Where(r => !string.IsNullOrWhiteSpace(r.PartNo))
            .Select(r => (s: Clean(r.PartNo), q: r.Quality))
            .ToList();
        if (items.Count == 0) return new VoteResult("", 0.0, 0, true);

        var strs = items.Select(i => i.s).ToList();
        int n = strs.Count;

        // 1) 완전일치 최빈값 단독 우세
        var ordered = strs.GroupBy(s => s)
                          .Select(g => (key: g.Key, cnt: g.Count()))
                          .OrderByDescending(x => x.cnt)
                          .ToList();
        string best;
        if (ordered.Count == 1 || ordered[0].cnt > ordered[1].cnt)
        {
            best = ordered[0].key;
        }
        else
        {
            // 2) medoid: 편집거리 합 최소, 동점이면 품질 높은 것
            best = items
                .OrderBy(it => strs.Sum(o => Lev(it.s, o)))
                .ThenByDescending(it => it.q)
                .First().s;
        }

        double agree = strs.Count(s => Lev(s, best) <= 1) / (double)n;
        bool needs = n < 3 || agree < 0.6;
        return new VoteResult(best, Math.Round(agree, 2), n, needs);
    }

    private static string Clean(string s) =>
        Regex.Replace(s, @"\s+", "").ToUpperInvariant();

    // Levenshtein 편집거리
    private static int Lev(string a, string b)
    {
        int m = a.Length, n = b.Length;
        var d = new int[n + 1];
        for (int j = 0; j <= n; j++) d[j] = j;
        for (int i = 1; i <= m; i++)
        {
            int prev = d[0];
            d[0] = i;
            for (int j = 1; j <= n; j++)
            {
                int tmp = d[j];
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1),
                                prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = tmp;
            }
        }
        return d[n];
    }
}
