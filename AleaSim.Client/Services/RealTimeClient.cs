using Microsoft.AspNetCore.SignalR.Client;
using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public class RealTimeClient : IAsyncDisposable {
    private HubConnection? _hubConnection;
    private readonly string _hubUrl = "http://localhost:5286/gamehub";

    public event Action<string, decimal>? OnJackpotUpdated;
    public event Action<decimal>? OnBalanceUpdated;
    public event Action<BigWinEventArgs>? OnBigWinReceived;
    public event Action<string, object>? OnLeaderboardUpdated;
    public event Action<object>? OnGameUpdateReceived; // Added back

    public async Task StartAsync(string token) {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string, decimal>("ReceiveJackpotUpdate", (name, value) => {
            OnJackpotUpdated?.Invoke(name, value);
        });

        _hubConnection.On<object>("ReceiveGameUpdate", (data) => {
            OnGameUpdateReceived?.Invoke(data);
            
            var json = data.ToString();
            if (json != null && json.Contains("Balance")) {
                // Simplified: extract balance or just trigger refresh
            }
        });

        _hubConnection.On<string, string, decimal, decimal>("ReceiveBigWin", (username, game, amount, mult) => {
            OnBigWinReceived?.Invoke(new BigWinEventArgs(username, game, amount, mult));
        });

        _hubConnection.On<string, object>("ReceiveLeaderboard", (name, data) => {
            OnLeaderboardUpdated?.Invoke(name, data);
        });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync() {
        if (_hubConnection != null) {
            await _hubConnection.DisposeAsync();
        }
    }
}

public record BigWinEventArgs(string Username, string GameName, decimal Amount, decimal Multiplier);