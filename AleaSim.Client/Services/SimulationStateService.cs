using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public class SimulationStateService
{
    public bool IsSimulationRunning { get; private set; }
    public string? CurrentGameType { get; private set; }
    public SimulationRequest? ActiveRequest { get; private set; }
    public DateTime? StartTime { get; private set; }

    public event Action? OnStateChanged;

    public void StartSimulation(SimulationRequest request)
    {
        IsSimulationRunning = true;
        ActiveRequest = request;
        CurrentGameType = request.GameType;
        StartTime = DateTime.UtcNow;
        NotifyStateChanged();
    }

    public void FinishSimulation()
    {
        IsSimulationRunning = false;
        ActiveRequest = null;
        CurrentGameType = null;
        StartTime = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
