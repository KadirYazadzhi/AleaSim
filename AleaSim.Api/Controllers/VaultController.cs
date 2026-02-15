using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VaultController : ControllerBase {
    private readonly IGameRepository _repo;
    private readonly IVaultService _vault;

    public VaultController(IGameRepository repo, IVaultService vault) {
        _repo = repo;
        _vault = vault;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("deposit")]
    public IActionResult Deposit([FromBody] decimal amount) {
        if (amount <= 0) return BadRequest("Invalid amount");
        
        try {
            var userId = GetUserIdOrThrow();
            var user = _repo.GetUser(userId);
            if (user == null) return NotFound();

            user.Balance += amount;
            _repo.UpdateUser(user);

            var tx = new Transaction {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Type = TransactionType.Deposit,
                Description = "Instant Deposit",
                Timestamp = DateTime.UtcNow,
                ResultingBalance = user.Balance
            };
            _repo.SaveTransaction(tx);

            return Ok(new { NewBalance = user.Balance });
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [HttpPost("faucet")]
    public IActionResult ClaimFaucet() {
        try {
            var userId = GetUserIdOrThrow();
            var user = _repo.GetUser(userId);
            if (user == null) return NotFound();

            if (user.Balance >= 10) {
                return BadRequest("Balance too high for faucet.");
            }

            var lastFaucet = _repo.GetUserTransactions(userId, 50)
                                  .Where(t => t.Type == TransactionType.Faucet)
                                  .OrderByDescending(t => t.Timestamp)
                                  .FirstOrDefault();

            if (lastFaucet != null && (DateTime.UtcNow - lastFaucet.Timestamp).TotalHours < 1) {
                var remainingMinutes = 60 - (int)(DateTime.UtcNow - lastFaucet.Timestamp).TotalMinutes;
                return BadRequest($"Faucet is cooling down. Try again in {remainingMinutes} minutes.");
            }

            decimal reliefAmount = 100;
            user.Balance += reliefAmount;
            _repo.UpdateUser(user);

            var tx = new Transaction {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = reliefAmount,
                Type = TransactionType.Faucet,
                Description = "Bankruptcy Relief",
                Timestamp = DateTime.UtcNow,
                ResultingBalance = user.Balance
            };
            _repo.SaveTransaction(tx);

            return Ok(new { NewBalance = user.Balance });
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [HttpGet("transactions")]
    public IActionResult GetTransactions() {
        try {
            var userId = GetUserIdOrThrow();
            var txs = _repo.GetUserTransactions(userId, 100);
            
            var result = txs.Select(t => new TransactionDto {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                Description = t.Description,
                Timestamp = t.Timestamp,
                ResultingBalance = t.ResultingBalance
            });

            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    private Guid GetUserIdOrThrow() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !Guid.TryParse(idClaim.Value, out var id)) {
            throw new UnauthorizedAccessException("Invalid User Token");
        }
        return id;
    }
}