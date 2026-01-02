using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class VoucherService : IVoucherService {
    
    public async Task<Voucher> CreateVoucher(string code, decimal amount, int maxUses, DateTime? expiry) {
        // Logic will be handled via repo in implementation
        return new Voucher { Code = code.ToUpper(), Amount = amount, MaxUses = maxUses, ExpiresAt = expiry };
    }

    public async Task<decimal> RedeemVoucher(Guid userId, string code, IGameRepository repo, IVaultService vault) {
        var voucher = repo.GetVoucherByCode(code.ToUpper());
        
        if (voucher == null || !voucher.IsActive) throw new Exception("Invalid or inactive voucher code.");
        if (voucher.ExpiresAt.HasValue && voucher.ExpiresAt < DateTime.UtcNow) throw new Exception("Voucher has expired.");
        if (voucher.CurrentUses >= voucher.MaxUses) throw new Exception("Voucher usage limit reached.");
        
        // Check if user already used this specific voucher
        if (repo.HasUserRedeemedVoucher(userId, voucher.Id)) throw new Exception("You have already redeemed this voucher.");

        // 1. Mark as used
        voucher.CurrentUses++;
        repo.UpdateVoucher(voucher);

        // 2. Record redemption
        repo.SaveUserVoucher(new UserVoucher {
            Id = Guid.NewGuid(),
            UserId = userId,
            VoucherId = voucher.Id,
            RedeemedAt = DateTime.UtcNow
        });

        // 3. Credit Bonus
        vault.CreditBonus(userId, voucher.Amount, voucher.Amount * 5, repo);

        return voucher.Amount;
    }

    public async Task<IEnumerable<Voucher>> GetAllVouchers(IGameRepository repo) {
        return repo.GetAllVouchers();
    }
}
