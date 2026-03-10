using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {

    public BlackjackGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
    }
    public class BlackjackHand {
        public List<string> Cards { get; set; } = new();
        public decimal Bet { get; set; }
        public bool IsDoubled { get; set; }
        public bool IsStand { get; set; }
        public bool IsBusted { get; set; }
        public bool IsBlackjack { get; set; }
        public bool IsSplitAces { get; set; } // Added restriction flag
    }

    public class BlackjackState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public List<string>? SplitHand { get; set; } = null;
        public int ActiveHandIndex { get; set; } = 0; // 0 = Main = 1 = Split
        public bool IsDoubleDown { get; set; } // Tracks main hand double
        public bool IsSplitDoubleDown { get; set; } // Tracks split hand double
        public bool IsSplitAces { get; set; } // Added: Tracks if split was on Aces
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");
            
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int seq = roundNum * 100;

            var state = new BlackjackState { BetAmount = lastBet.Amount, Sequence = seq };
            
            // Check for forced directives
            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, lastBet.Amount, repo);

            state.PlayerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.PlayerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.Sequence = seq;

            if (CalculateHandValue(state.PlayerHand) == 21) {
                // Blackjack! Check dealer right away.
                await FinishRoundAsync(session, state, repo, questService);
            }

            int roundCount = repo.GetRoundCount(sessionId);
            RotateServerSeed(session, roundCount);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = lastBet.Amount,
                ExecutedAt = DateTime.UtcNow,
                RandomResult = JsonSerializer.Serialize(state),
                ServerSeed = session.ServerSeed ?? "",
                ServerSeedHash = session.ServerSeedHash ?? "",
                ClientSeed = session.ClientSeed ?? "",
                Nonce = state.Sequence,
                DecisionType = directive.DecisionType
            };
            
            // If instant finish (Blackjack)
            if (state.IsRoundOver) round.TotalWinAmount = (CalculateHandValue(state.PlayerHand) == 21 && CalculateHandValue(state.DealerHand) != 21) ? state.BetAmount * 2.5m : state.BetAmount; 

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Blackjack", State = state });
            return round;
        });
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) return;
            var round = repo.GetLastRound(sessionId);
            if (round == null) return;
            var state = JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
            if (state == null || state.IsRoundOver) return;

            var targetHand = (state.ActiveHandIndex == 1 && state.SplitHand != null) ? state.SplitHand : state.PlayerHand;

            if (action.ToLower() == "double" && targetHand.Count == 2) {
                if (await VaultService.ProcessBetAsync(session.UserId, state.BetAmount, repo)) {
                    repo.UpdateRtpStats(session.GameId, session.UserId, state.BetAmount, 0); 
                    
                    if (state.ActiveHandIndex == 1) state.IsSplitDoubleDown = true;
                    else state.IsDoubleDown = true;

                    int seq = state.Sequence;
                    targetHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                    state.Sequence = seq;
                    
                    if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                        state.ActiveHandIndex = 1; // Move to next hand
                    } else {
                        await FinishRoundAsync(session, state, repo, questService);
                    }
                } else throw new Exception("Insufficient funds for Double Down");
            } 
            else if (action.ToLower() == "split" && state.PlayerHand.Count == 2 && state.SplitHand == null) {
                string r1 = state.PlayerHand[0].Substring(0, state.PlayerHand[0].Length - 1);
                string r2 = state.PlayerHand[1].Substring(0, state.PlayerHand[1].Length - 1);
                bool isTenValue1 = (r1 == "10" || r1 == "J" || r1 == "Q" || r1 == "K");
                bool isTenValue2 = (r2 == "10" || r2 == "J" || r2 == "Q" || r2 == "K");
                
                if (r1 == r2 || (isTenValue1 && isTenValue2)) {
                    if (await VaultService.ProcessBetAsync(session.UserId, state.BetAmount, repo)) {
                        repo.UpdateRtpStats(session.GameId, session.UserId, state.BetAmount, 0);
                        state.SplitHand = new List<string> { state.PlayerHand[1] };
                        state.PlayerHand.RemoveAt(1);
                        if (r1 == "A") state.IsSplitAces = true;
                        int seq = state.Sequence;
                        state.PlayerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                        if (state.SplitHand != null) state.SplitHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                        state.Sequence = seq;
                    } else throw new Exception("Insufficient funds for Split");
                }
            } 
            else if (action.ToLower() == "insurance" && state.DealerHand.Count == 2 && state.DealerHand[0].StartsWith("A") && !state.IsRoundOver) {
                decimal insuranceBet = state.BetAmount / 2;
                if (await VaultService.ProcessBetAsync(session.UserId, insuranceBet, repo)) {
                    // Check dealer hole card
                    if (CalculateHandValue(state.DealerHand) == 21) {
                        // Dealer has BJ, insurance pays 2:1
                        decimal win = insuranceBet * 3; // Return bet + win (2x)
                        
                        repo.UpdateGamePoolBalance(session.GameId, -win);

                        await VaultService.ProcessWinAsync(session.UserId, win, repo);
                        repo.UpdateRtpStats(session.GameId, session.UserId, insuranceBet, win);
                    } else {
                        // Dealer does not have BJ, insurance lost.
                        repo.UpdateRtpStats(session.GameId, session.UserId, insuranceBet, 0);
                    }
                } else throw new Exception("Insufficient funds for Insurance");
            }
            else if (action.ToLower() == "hit") {
                if (state.IsSplitAces && targetHand.Count >= 2) throw new Exception("Cannot hit on split Aces.");
                int seq = state.Sequence;
                targetHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                state.Sequence = seq;
                if (CalculateHandValue(targetHand) >= 21) {
                    if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                        state.ActiveHandIndex = 1; // Move to next hand on bust/21
                    } else {
                        await FinishRoundAsync(session, state, repo, questService);
                    }
                }
            } 
            else if (action.ToLower() == "stand") {
                if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                    state.ActiveHandIndex = 1; // Move to next hand
                } else {
                    await FinishRoundAsync(session, state, repo, questService);
                }
            }

            // Update Round
            round.RandomResult = JsonSerializer.Serialize(state);
            // If round is over, update TotalWinAmount here for persistence
            if (state.IsRoundOver) {
                decimal win = CalculateWin(state);
                round.TotalWinAmount = win;
            }

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Action = action, State = state });
        });
    }

    private async Task FinishRoundAsync(GameSession session, BlackjackState state, IGameRepository repo, IQuestService questService) {
        if (session == null) return;
        state.IsRoundOver = true;
        int pVal = CalculateHandValue(state.PlayerHand);
        int dVal = CalculateHandValue(state.DealerHand);
        
        bool hasSplit = state.SplitHand != null;
        int sVal = hasSplit ? CalculateHandValue(state.SplitHand!) : 0;

        // DEALER LOGIC FIX: Dealer plays if ANY player hand is not busted (<= 21)
        bool pAlive = pVal <= 21;
        bool sAlive = hasSplit && sVal <= 21;

        if (pAlive || sAlive) {
            int seq = state.Sequence;
            while (dVal < 17) {
                state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                dVal = CalculateHandValue(state.DealerHand);
            }
            state.Sequence = seq;
        }

        decimal win = CalculateWin(state);

        if (win > 0) {
            repo.UpdateGamePoolBalance(session.GameId, -win);
            await VaultService.ProcessWinAsync(session.UserId, win, repo);
            repo.UpdateRtpStats(session.GameId, session.UserId, 0, win);
            await questService.UpdateProgressAsync(session.UserId, "WinAmount", win, repo, RealTimeService, VaultService);
        }
        BrainService.UpdateProfile(session.UserId, state.BetAmount, win, repo);
    }

    private decimal CalculateWin(BlackjackState state) {
        int pVal = CalculateHandValue(state.PlayerHand);
        int dVal = CalculateHandValue(state.DealerHand);
        decimal win = 0;
        decimal bet1 = state.IsDoubleDown ? state.BetAmount * 2 : state.BetAmount;
        
        // Main Hand
        if (pVal <= 21) {
            bool isBJ = pVal == 21 && state.PlayerHand.Count == 2;
            bool dBJ = dVal == 21 && state.DealerHand.Count == 2;

            if (isBJ) {
                if (dBJ) win += bet1; // Push
                else win += bet1 * 2.5m; // 3:2 Payout
            }
            else {
                if (dVal > 21 || pVal > dVal) win += bet1 * 2;
                else if (pVal == dVal) win += bet1;
            }
        }

        // Split Hand
        if (state.SplitHand != null) {
            int sVal = CalculateHandValue(state.SplitHand);
            decimal bet2 = state.IsSplitDoubleDown ? state.BetAmount * 2 : state.BetAmount;
            
            if (sVal <= 21) {
                if (dVal > 21 || sVal > dVal) win += bet2 * 2;
                else if (sVal == dVal) win += bet2;
            }
        }
        return win;
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

    private int CalculateHandValue(List<string> hand) {
        int value = 0;
        int aces = 0;
        foreach (var card in hand) {
            if (string.IsNullOrEmpty(card)) continue;
            string rank = card.Substring(0, card.Length - 1);
            if (rank == "A") aces++;
            else if (rank == "J" || rank == "Q" || rank == "K") value += 10;
            else if (int.TryParse(rank, out int val)) value += val;
        }
        for (int i = 0; i < aces; i++) {
            if (value + 11 <= 21) value += 11;
            else value += 1;
        }
        return value;
    }
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        if (LockService is null) return null;
        return await ExecuteScopedAsync((repo, _, _) => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return Task.FromResult<object?>(null);
            return Task.FromResult<object?>(JsonSerializer.Deserialize<BlackjackState>(round.RandomResult));
        });
    }
}
