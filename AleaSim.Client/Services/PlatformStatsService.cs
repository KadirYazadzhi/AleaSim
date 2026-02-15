using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public class PlatformStatsService : IDisposable
{
    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;
    private PlatformStatsDto _stats = new() { WeeklyJackpot = 25000, ActivePlayers = 1200, AverageRtp = 98.4, TotalRewardsPaid = 4200000 };

    public PlatformStatsDto Stats => _stats;

    public event Action? OnStatsUpdated;

    public PlatformStatsService(HttpClient http)
    {
        _http = http;
    }

    public void StartPolling()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _ = PollTask(_cts.Token);
    }

    private async Task PollTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var newStats = await _http.GetFromJsonAsync<PlatformStatsDto>("api/Game/platform-stats", token);
                if (newStats != null)
                {
                    _stats = newStats;
                    OnStatsUpdated?.Invoke();
                }
            }
            catch { /* Ignore polling errors */ }

            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
