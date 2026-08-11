using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using EpsonScanApi.Models;

namespace EpsonScanApi.Services;

public class JobRegistry
{
    private readonly ConcurrentDictionary<string, JobModel> _jobs = new();
    private string? _persistPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions Jso = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Configure(string? persistPath)
    {
        _persistPath = persistPath;
        if (persistPath == null || !File.Exists(persistPath)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<JobModel>>(
                File.ReadAllText(persistPath), Jso) ?? new();
            foreach (var j in list)
                _jobs[j.Id] = j;
        }
        catch { }
    }

    public JobModel Create(string status = "created", object? @params = null)
    {
        var jid = Guid.NewGuid().ToString("N")[..12];
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var job = new JobModel { Id = jid, Status = status, CreatedAt = now, UpdatedAt = now, Params = @params };
        _jobs[jid] = job;
        _ = SaveAsync();
        return Clone(job);
    }

    public JobModel? Update(string jid, Action<JobModel> updater)
    {
        if (!_jobs.TryGetValue(jid, out var job)) return null;
        updater(job);
        job.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _ = SaveAsync();
        return Clone(job);
    }

    public JobModel? Get(string jid)
        => _jobs.TryGetValue(jid, out var j) ? Clone(j) : null;

    public List<JobModel> ListAll()
        => _jobs.Values.Select(Clone).OrderByDescending(j => j.CreatedAt).ToList();

    public bool Delete(string jid)
    {
        var ok = _jobs.TryRemove(jid, out _);
        if (ok) _ = SaveAsync();
        return ok;
    }

    private async Task SaveAsync()
    {
        if (_persistPath == null) return;
        await _saveLock.WaitAsync();
        try { await File.WriteAllTextAsync(_persistPath, JsonSerializer.Serialize(_jobs.Values.ToList(), Jso)); }
        catch { }
        finally { _saveLock.Release(); }
    }

    private static JobModel Clone(JobModel j)
    {
        var json = JsonSerializer.Serialize(j, Jso);
        return JsonSerializer.Deserialize<JobModel>(json, Jso)!;
    }
}
