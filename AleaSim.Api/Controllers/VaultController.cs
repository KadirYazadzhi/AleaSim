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
public class VaultController : BaseApiController {
    private readonly IGameRepository _repo;
    private readonly IVaultService _vault;
    private readonly ILockService _lockService;
    private readonly AleaSim.Domain.Services.IRedisCacheService _redisCache;

    public VaultController(IGameRepository repo, IVaultService vault, ILockService lockService, AleaSim.Domain.Services.IRedisCacheService redisCache) {
        _repo = repo;
        _vault = vault;
        _lockService = lockService;
        _redisCache = redisCache;
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
    public async Task<IActionResult> ClaimFaucet() {
        try {
            var userId = GetUserIdOrThrow();

            // 1. Rate Limit (1 per hour via Redis)
            if (await _redisCache.IncrementRateLimitAsync($"ratelimit:faucet:{userId}", TimeSpan.FromHours(1), 1)) {
                return BadRequest("Faucet is cooling down. Please wait 1 hour.");
            }

            // 2. Distributed Lock
            using var lockHandle = await _lockService.AcquireLockAsync($"faucet_lock_{userId}", TimeSpan.FromSeconds(5));

            var user = _repo.GetUser(userId);
            if (user == null) return NotFound();

            if (user.Balance >= 10) {
                return BadRequest("Balance too high for faucet.");
            }

            // 3. Double Check with DB History
            var lastFaucet = _repo.GetUserTransactions(userId, 10)
                                  .Where(t => t.Type == TransactionType.Faucet)
                                  .OrderByDescending(t => t.Timestamp)
                                  .FirstOrDefault();

            if (lastFaucet != null && (DateTime.UtcNow - lastFaucet.Timestamp).TotalHours < 1) {
                return BadRequest("Faucet cooldown in progress.");
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
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
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

    [HttpGet("cashback")]
    public IActionResult GetPendingCashback() {
        try {
            var userId = GetUserIdOrThrow();
            var amount = _vault.GetPendingCashback(userId, _repo);
            return Ok(new { Amount = amount });
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [HttpPost("cashback/claim")]
    public IActionResult ClaimCashback() {
        try {
            var userId = GetUserIdOrThrow();
            var amount = _vault.ClaimCashbackAsync(userId, _repo).GetAwaiter().GetResult();
            
            if (amount <= 0) return BadRequest("No pending cashback to claim.");
            
            return Ok(new { ClaimedAmount = amount, Message = $"Successfully claimed {amount:C} cashback!" });
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    private Guid GetUserIdOrThrow() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !Guid.TryParse(idClaim.Value, out var id)) {
            throw new UnauthorizedAccessException("Invalid User Token");
        }

        if (_repo.GetUser(id) == null) {
            throw new UnauthorizedAccessException("User no longer exists");
        }

        return id;
    }
}