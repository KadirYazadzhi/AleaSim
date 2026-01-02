using AleaSim.Shared.Models;
using System.Net.Http.Json;

namespace AleaSim.Client.Services;

public interface IGameService {
    Task<StartSessionResponse> StartSession(string gameType);
    Task<PlaceBetResponse> PlaceBet(string gameType, Guid sessionId, decimal amount, string betData = "{}");
    Task<GameActionResponse> PerformAction(string gameType, Guid sessionId, string action, string actionData = "{}");
}

public class GameService : IGameService {
    private readonly HttpClient _http;

    public GameService(HttpClient http) {
        _http = http;
    }

    public async Task<StartSessionResponse> StartSession(string gameType) {
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/session", new StartSessionRequest());
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<StartSessionResponse>() ?? throw new Exception("Invalid response");
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<PlaceBetResponse> PlaceBet(string gameType, Guid sessionId, decimal amount, string betData = "{}") {
        var request = new PlaceBetRequest { Amount = amount, BetData = betData };
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/bet/{sessionId}", request);
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<PlaceBetResponse>() ?? throw new Exception("Invalid response");
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<GameActionResponse> PerformAction(string gameType, Guid sessionId, string action, string actionData = "{}") {
        var request = new GameActionRequest { Action = action, ActionData = actionData };
        var response = await _http.PostAsJsonAsync($"api/Game/{gameType}/action/{sessionId}", request);
        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadFromJsonAsync<GameActionResponse>() ?? throw new Exception("Invalid response");
        }
        throw new Exception(await response.Content.ReadAsStringAsync());
    }
}
