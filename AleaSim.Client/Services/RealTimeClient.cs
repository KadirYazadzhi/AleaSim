using Microsoft.AspNetCore.SignalR.Client;
using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public class RealTimeClient : IAsyncDisposable {
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    public RealTimeClient(Microsoft.AspNetCore.Components.NavigationManager navigationManager) {
        var baseUri = navigationManager.BaseUri;
        
        // If we are running on a typical Blazor dev port (like 5241), the API is likely on 5286
        if (baseUri.Contains(":5241")) {
            baseUri = baseUri.Replace(":5241", ":5286");
        } else if (baseUri.Contains(":7241")) {
            baseUri = baseUri.Replace(":7241", ":7286");
        }

        _hubUrl = new Uri(new Uri(baseUri), "gamehub").ToString();
    }

    public event Action<JackpotDto>? OnJackpotUpdated;
    public event Action<decimal, decimal>? OnBalanceUpdated;
    public event Action<BigWinEventArgs>? OnBigWinReceived;
    public event Action<string, object>? OnLeaderboardUpdated;
    public event Action<object>? OnGameUpdateReceived;
    public event Action<string, string, DateTime, string>? OnChatMessageReceived; // Added avatar

    public async Task StartAsync(string token) {
        if (_hubConnection != null) {
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        _hubConnection.On<JackpotDto>("ReceiveJackpotUpdate", (dto) => {
            OnJackpotUpdated?.Invoke(dto);
        });

        _hubConnection.On<BalanceUpdateDto>("ReceiveBalanceUpdate", (dto) => {
            OnBalanceUpdated?.Invoke(dto.Balance, dto.BonusBalance);
        });

        _hubConnection.On<string, string, DateTime, string>("ReceiveChatMessage", (user, msg, time, avatar) => {
            OnChatMessageReceived?.Invoke(user, msg, time, avatar);
        });

        _hubConnection.On<object>("ReceiveGameUpdate", (data) => {
            OnGameUpdateReceived?.Invoke(data);
        });

        _hubConnection.On<string, string, decimal, decimal>("ReceiveBigWin", (username, game, amount, mult) => {
            OnBigWinReceived?.Invoke(new BigWinEventArgs(username, game, amount, mult));
        });

        _hubConnection.On<string, object>("ReceiveLeaderboard", (name, data) => {
            OnLeaderboardUpdated?.Invoke(name, data);
        });

        await _hubConnection.StartAsync();
    }

    public async Task SendChatMessage(string message) {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected) {
            await _hubConnection.SendAsync("SendMessage", message);
        }
    }

    public async ValueTask DisposeAsync() {
        if (_hubConnection != null) {
            await _hubConnection.DisposeAsync();
        }
    }
}

public record BigWinEventArgs(string Username, string GameName, decimal Amount, decimal Multiplier);
