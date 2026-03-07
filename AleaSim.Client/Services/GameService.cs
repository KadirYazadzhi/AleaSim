using AleaSim.Shared.Models;
using System.Net.Http.Json;

namespace AleaSim.Client.Services;

public interface IGameService {
    event Action<float>? OnFlowStateChanged;
    Task<StartSessionResponse> StartSession(string gameType);
    Task<StartSessionResponse?> ResumeSession(string gameType); // Added
    Task<PlaceBetResponse> PlaceBet(string gameType, Guid sessionId, decimal amount, object betData);
    Task<GameActionResponse> PerformAction(string gameType, Guid sessionId, string action, object? actionData = null);
    Task<DailyBonusResponse> SpinDailyBonus();
    Task<bool> RedeemVoucher(string code);
    Task<List<LeaderboardEntry>> GetLeaderboard(string timeFrame = "daily");
    Task<List<GameHistoryDto>> GetHistory(int count = 20);
}

public class GameService : IGameService {
    private readonly HttpClient _http;
    public event Action<float>? OnFlowStateChanged;

    public GameService(HttpClient http) {
        _http = http;
    }

    public async Task<StartSessionResponse?> ResumeSession(string gameType) {
        var response = await _http.GetAsync($"api/Game/{gameType}/session");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<StartSessionResponse>();
        }
        return null;
    }

    public async Task<StartSessionResponse> StartSession(string gameType) {
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/session", new StartSessionRequest());
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<StartSessionResponse>() ?? throw new Exception("Invalid response");
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<PlaceBetResponse> PlaceBet(string gameType, Guid sessionId, decimal amount, object betData) {
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/bet/{sessionId}", new { Amount = amount, BetData = betData });
        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<PlaceBetResponse>() ?? throw new Exception("Invalid response");
            
            // Trigger Flow State Update
            OnFlowStateChanged?.Invoke(result.FlowStateIntensity);
            
            return result;
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }


    public async Task<GameActionResponse> PerformAction(string gameType, Guid sessionId, string action, object? actionData = null) {
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/action/{sessionId}", new { Action = action, ActionData = actionData });
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<GameActionResponse>() ?? throw new Exception("Invalid response");
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<DailyBonusResponse> SpinDailyBonus() {
        var response = await _http.PostAsync("api/Game/daily-bonus", null);
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<DailyBonusResponse>() ?? new DailyBonusResponse();
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> RedeemVoucher(string code) {
        var response = await _http.PostAsJsonAsync("api/Game/voucher/redeem", new { Code = code });
        return response.IsSuccessStatusCode;
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboard(string timeFrame = "daily") {
        return await _http.GetFromJsonAsync<List<LeaderboardEntry>>($"api/Game/leaderboard/{timeFrame}") ?? new();
    }

    public async Task<List<GameHistoryDto>> GetHistory(int count = 20) {
         return await _http.GetFromJsonAsync<List<GameHistoryDto>>($"api/Game/history?count={count}") ?? new();
    }
}
