using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class VoucherService : IVoucherService {
    
    public async Task<Voucher> CreateVoucher(string code, decimal amount, int maxUses, DateTime? expiry) {
        // Logic will be handled via repo in implementation
        return new Voucher { Code = code.ToUpper(), Amount = amount, MaxUses = maxUses, ExpiresAt = expiry };
    }

    public async Task<decimal> RedeemVoucher(Guid userId, string code, IGameRepository repo, IVaultService vaultService) {
        var voucher = repo.GetVoucherByCode(code);
        if (voucher == null) throw new Exception("Invalid voucher code.");
        if (voucher.ExpiresAt < DateTime.UtcNow) throw new Exception("Voucher expired.");
        if (voucher.CurrentUses >= voucher.MaxUses) throw new Exception("Voucher fully redeemed.");
        
        if (repo.HasUserRedeemedVoucher(userId, voucher.Id)) throw new Exception("You have already redeemed this voucher.");

        using var transaction = repo.BeginTransaction();
        try {
            voucher.CurrentUses++;
            repo.UpdateVoucher(voucher);

            var userVoucher = new UserVoucher {
                Id = Guid.NewGuid(),
                UserId = userId,
                VoucherId = voucher.Id,
                RedeemedAt = DateTime.UtcNow
            };
            repo.SaveUserVoucher(userVoucher);

            // Vault service usually saves changes, but we are inside a transaction scope now.
            await vaultService.CreditBonusAsync(userId, voucher.Amount, voucher.Amount * 5, repo); 
            
            repo.SaveChanges();
            transaction.Commit();
            
            return voucher.Amount;
        }
        catch {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Voucher>> GetAllVouchers(IGameRepository repo) {
        return repo.GetAllVouchers();
    }
}
