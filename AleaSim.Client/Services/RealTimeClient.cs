using Microsoft.AspNetCore.SignalR.Client;
using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public class RealTimeClient : IAsyncDisposable {
    private HubConnection? _hubConnection;
    private readonly string _hubUrl = "http://localhost:5286/gamehub";

    // Events for UI components to subscribe to
    public event Action<string, decimal>? OnJackpotUpdated;
    public event Action<object>? OnGameUpdateReceived;
    public event Action<string, string, decimal, decimal>? OnBigWinReceived;

    public async Task StartAsync(string token) {
        if (_hubConnection != null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("ReceiveJackpotUpdate", (data) => {
            // Logic to parse dynamic object or use specific DTO
            // For now, invoking event
        });

        _hubConnection.On<object>("ReceiveGameUpdate", (data) => {
            OnGameUpdateReceived?.Invoke(data);
        });

        _hubConnection.On<object>("ReceiveBigWin", (data) => {
            // Logic to notify
        });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync() {
        if (_hubConnection != null) {
            await _hubConnection.DisposeAsync();
        }
    }
}
