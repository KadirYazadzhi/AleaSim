using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IVoucherService {
    Task<Voucher> CreateVoucher(string code, decimal amount, int maxUses, DateTime? expiry);
    Task<decimal> RedeemVoucher(Guid userId, string code, IGameRepository repo, IVaultService vault);
    Task<IEnumerable<Voucher>> GetAllVouchers(IGameRepository repo);
}
