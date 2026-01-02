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

    [HttpPost("deposit")]
    public IActionResult Deposit([FromBody] decimal amount) {
        if (amount <= 0) return BadRequest("Invalid amount");
        
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
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

    [HttpGet("transactions")]
    public IActionResult GetTransactions() {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var txs = _repo.GetUserTransactions(userId, 50);
        
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
}
