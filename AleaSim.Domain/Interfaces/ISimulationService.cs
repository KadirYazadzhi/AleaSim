using AleaSim.Shared.Models;

namespace AleaSim.Domain.Interfaces;

public interface ISimulationService {
    Task<SimulationReport> RunSimulation(SimulationRequest request);
}
