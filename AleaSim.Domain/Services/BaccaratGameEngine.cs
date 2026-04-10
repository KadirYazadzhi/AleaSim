using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class BaccaratGameEngine : BaseGameEngine {

    public BaccaratGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData) {
        try {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(betData ?? "{}");
            string betType = data?.GetValueOrDefault("Type") ?? "Player";
            
            var validTypes = new[] { "Player", "Banker", "Tie" };
            if (!validTypes.Contains(betType)) throw new Exception($"Invalid bet type: {betType}");

            if (amount < 1 || amount > 5000) throw new Exception("Bet amount must be between 1 and 5000.");
        } catch { throw new Exception("Security Alert: Invalid bet data format."); }

        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public class BaccaratState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> BankerHand { get; set; } = new();
        public int PlayerScore { get; set; }
        public int BankerScore { get; set; }
        public string Outcome { get; set; } = ""; // "Player", "Banker", "Tie"
        public string BetType { get; set; } = ""; // "Player", "Banker", "Tie"
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));

        var round = await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");
            
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int seq = roundNum * 100;

            var betData = JsonSerializer.Deserialize<Dictionary<string, string>>(lastBet.BetData ?? "{}");
            string betType = betData?.GetValueOrDefault("Type") ?? "Player";

            var state = new BaccaratState { 
                BetAmount = lastBet.Amount, 
                BetType = betType,
                Sequence = seq 
            };
            
            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, lastBet.Amount, repo);

            // Initial Deal
            state.PlayerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.BankerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.PlayerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.BankerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));

            state.PlayerScore = CalculateScore(state.PlayerHand);
            state.BankerScore = CalculateScore(state.BankerHand);

            // Natural Win Check (8 or 9)
            bool isNatural = state.PlayerScore >= 8 || state.BankerScore >= 8;

            if (!isNatural) {
                // Player's Rule
                bool playerDrewThird = false;
                int? playerThirdCardVal = null;

                if (state.PlayerScore <= 5) {
                    string card = DrawCard(session.ServerSeed, session.ClientSeed, ref seq);
                    state.PlayerHand.Add(card);
                    playerDrewThird = true;
                    playerThirdCardVal = GetCardValue(card);
                    state.PlayerScore = CalculateScore(state.PlayerHand);
                }

                // Banker's Rule
                bool bankerDraws = false;
                if (!playerDrewThird) {
                    if (state.BankerScore <= 5) bankerDraws = true;
                } else {
                    // Banker draws based on Player's third card
                    int b = state.BankerScore;
                    int p3 = playerThirdCardVal!.Value;

                    if (b <= 2) bankerDraws = true;
                    else if (b == 3 && p3 != 8) bankerDraws = true;
                    else if (b == 4 && (p3 >= 2 && p3 <= 7)) bankerDraws = true;
                    else if (b == 5 && (p3 >= 4 && p3 <= 7)) bankerDraws = true;
                    else if (b == 6 && (p3 == 6 || p3 == 7)) bankerDraws = true;
                }

                if (bankerDraws) {
                    state.BankerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                    state.BankerScore = CalculateScore(state.BankerHand);
                }
            }

            state.Sequence = seq;
            state.IsRoundOver = true;

            if (state.PlayerScore > state.BankerScore) state.Outcome = "Player";
            else if (state.BankerScore > state.PlayerScore) state.Outcome = "Banker";
            else state.Outcome = "Tie";

            decimal win = CalculateWin(state);

            var roundId = Guid.NewGuid();

            var shadowDirective = BrainService.DecideOutcome(session.UserId, session.GameId, lastBet.Amount, repo, isShadowMode: true);

            var round = new GameRound {
                Id = roundId,
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = lastBet.Amount,
                TotalWinAmount = win,
                ExecutedAt = DateTime.UtcNow,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDirective),
                RandomResult = JsonSerializer.Serialize(state),
                ServerSeed = session.ServerSeed ?? "",
                ServerSeedHash = session.ServerSeedHash ?? "",
                ClientSeed = session.ClientSeed ?? "",
                Nonce = state.Sequence,
                DecisionType = directive.DecisionType
            };

            if (win > 0) {
                repo.UpdateGamePoolBalance(session.GameId, -win);
                await VaultService.ProcessWinAsync(session.UserId, win, repo, roundId);
                repo.UpdateRtpStats(session.GameId, session.UserId, 0, win);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", win, repo, RealTimeService, VaultService);
            }

            await BrainService.UpdateProfileAsync(session.UserId, lastBet.Amount, win, repo);
            
            int roundCount = repo.GetRoundCount(sessionId);
            RotateServerSeed(session, roundCount);

            repo.SaveRound(round);
            repo.UpdateSession(session);
            
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Baccarat", State = state });
            
            if (win >= lastBet.Amount * 5) {
                var user = repo.GetUser(session.UserId);
                var game = repo.GetGame(session.GameId);
                await RealTimeService.NotifyBigWin(user?.Username ?? "Guest", game?.Name ?? "Baccarat", win, win / lastBet.Amount);
            }

            return round;
        });

        // Sync Cache AFTER Transaction Commit
        using (var scope = ScopeFactory.CreateScope()) {
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var session = repo.GetSession(sessionId);
            if (session != null) await BrainService.SyncProfileToCacheAsync(session.UserId, repo).ConfigureAwait(false);
        }

        return round;
    }

    private decimal CalculateWin(BaccaratState state) {
        if (state.Outcome == state.BetType) {
            return state.BetType switch {
                "Player" => state.BetAmount * 2,
                "Banker" => state.BetAmount * 1.95m, // 5% Commission
                "Tie" => state.BetAmount * 9,       // 8:1 Payout
                _ => 0
            };
        }
        return 0;
    }

    private int CalculateScore(List<string> hand) {
        int total = hand.Sum(c => GetCardValue(c));
        return total % 10;
    }

    private int GetCardValue(string card) {
        string rank = card.Substring(0, card.Length - 1);
        if (rank == "A") return 1;
        if (rank == "10" || rank == "J" || rank == "Q" || rank == "K") return 0;
        return int.Parse(rank);
    }

    private string DrawCard(string serverSeed, string clientSeed, ref int seq) {
        seq++;
        int idx = RngService.GetNextInt(serverSeed, clientSeed, seq, 0, 52);
        int rankIdx = idx % 13;
        int suitIdx = idx / 13;
        
        string rank = rankIdx switch {
            0 => "A",
            9 => "10",
            10 => "J",
            11 => "Q",
            12 => "K",
            _ => (rankIdx + 1).ToString()
        };
        
        string suit = "HDCS"[suitIdx].ToString();
        return rank + suit;
    }

    public override Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => Task.CompletedTask;

    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await ExecuteScopedAsync((repo, _, _) => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return Task.FromResult<object?>(null);
            return Task.FromResult<object?>(JsonSerializer.Deserialize<BaccaratState>(round.RandomResult));
        });
    }

    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
}