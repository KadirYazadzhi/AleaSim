using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public BlackjackGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {   
    }

    public class BlackjackState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public List<string>? SplitHand { get; set; } = null;
        public int ActiveHandIndex { get; set; } = 0; // 0 = Main, 1 = Split
        public bool IsDoubleDown { get; set; }
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");
            
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int seq = roundNum * 100; // Offset sequence by round number

            var state = new BlackjackState { BetAmount = lastBet.Amount, Sequence = seq };
            state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
            state.DealerHand.Add(DrawCard(session.Seed, ref seq));
            state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
            state.DealerHand.Add(DrawCard(session.Seed, ref seq));
            state.Sequence = seq;

            if (CalculateHandValue(state.PlayerHand) == 21) state.IsRoundOver = true;

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = lastBet.Amount,
                ExecutedAt = DateTime.UtcNow,
                RandomResult = JsonSerializer.Serialize(state)
            };

            if (state.IsRoundOver) await FinishRoundAsync(session, round, state, repo, questService);

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Blackjack", State = state });
            return round;
        });
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            var round = repo.GetLastRound(sessionId);
            if (round == null) return;
            var state = JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
            if (state == null || state.IsRoundOver) return;

            var targetHand = (state.ActiveHandIndex == 1 && state.SplitHand != null) ? state.SplitHand : state.PlayerHand;

            if (action.ToLower() == "double" && targetHand.Count == 2) {
                if (VaultService.ProcessBet(session.UserId, state.BetAmount, repo)) {
                    state.IsDoubleDown = true;
                    int seq = state.Sequence;
                    targetHand.Add(DrawCard(session.Seed, ref seq));
                    state.Sequence = seq;
                    
                    if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                        state.ActiveHandIndex = 1; // Move to next hand
                    } else {
                        await FinishRoundAsync(session, round, state, repo, questService);
                    }
                } else throw new Exception("Insufficient funds for Double Down");
            } else if (action.ToLower() == "split" && state.PlayerHand.Count == 2 && state.SplitHand == null) {
                 // Check if cards are same rank
                string r1 = state.PlayerHand[0].Substring(0, state.PlayerHand[0].Length - 1);
                string r2 = state.PlayerHand[1].Substring(0, state.PlayerHand[1].Length - 1);
                // Allow splitting any 10-value card (e.g. J and K)
                bool isTenValue1 = (r1 == "10" || r1 == "J" || r1 == "Q" || r1 == "K");
                bool isTenValue2 = (r2 == "10" || r2 == "J" || r2 == "Q" || r2 == "K");
                
                if (r1 == r2 || (isTenValue1 && isTenValue2)) {
                    if (VaultService.ProcessBet(session.UserId, state.BetAmount, repo)) {
                        state.SplitHand = new List<string> { state.PlayerHand[1] };
                        state.PlayerHand.RemoveAt(1);
                        int seq = state.Sequence;
                        state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
                        state.SplitHand.Add(DrawCard(session.Seed, ref seq));
                        state.Sequence = seq;
                    } else throw new Exception("Insufficient funds for Split");
                }
            } else if (action.ToLower() == "hit") {
                int seq = state.Sequence;
                targetHand.Add(DrawCard(session.Seed, ref seq));
                state.Sequence = seq;
                if (CalculateHandValue(targetHand) >= 21) {
                    if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                        state.ActiveHandIndex = 1; // Move to next hand on bust/21
                    } else {
                        await FinishRoundAsync(session, round, state, repo, questService);
                    }
                }
            } else if (action.ToLower() == "stand") {
                if (state.SplitHand != null && state.ActiveHandIndex == 0) {
                    state.ActiveHandIndex = 1; // Move to next hand
                } else {
                    await FinishRoundAsync(session, round, state, repo, questService);
                }
            }

            round.RandomResult = JsonSerializer.Serialize(state);
            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Action = action, State = state });
        });
    }

    private async Task FinishRoundAsync(GameSession session, GameRound round, BlackjackState state, IGameRepository repo, IQuestService questService) {
        state.IsRoundOver = true;
        int pVal = CalculateHandValue(state.PlayerHand);
        int dVal = CalculateHandValue(state.DealerHand);
        decimal win = 0;
        decimal currentBet = state.IsDoubleDown ? state.BetAmount * 2 : state.BetAmount;
        
        // Dealer plays if player didn't bust
        if (pVal <= 21 || (state.SplitHand != null && CalculateHandValue(state.SplitHand) <= 21)) {
            int seq = state.Sequence;
            while (dVal < 17) {
                state.DealerHand.Add(DrawCard(session.Seed, ref seq));
                dVal = CalculateHandValue(state.DealerHand);
            }
            state.Sequence = seq;
        }

        // Main Hand
        if (pVal <= 21) {
            if (dVal > 21 || pVal > dVal) win += currentBet * 2;
            else if (pVal == dVal) win += currentBet;
        }

        // Split Hand
        if (state.SplitHand != null) {
            int sVal = CalculateHandValue(state.SplitHand);
            if (sVal <= 21) {
                if (dVal > 21 || sVal > dVal) win += state.BetAmount * 2;
                else if (sVal == dVal) win += state.BetAmount;
            }
        }

        if (win > 0) {
            VaultService.ProcessWin(session.UserId, win, repo);
            questService.UpdateProgress(session.UserId, "WinAmount", (int)win, repo, VaultService);
        }
        BrainService.UpdateProfile(session.UserId, state.BetAmount, win);
        round.TotalWinAmount = win;
        await Task.CompletedTask;
    }

    private string DrawCard(int seed, ref int seq) {
        seq++;
        int idx = RngService.GetNextInt(seed, seq, 0, 52);
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
            string rank = card.Substring(0, card.Length - 1); // Extract rank (handle 10)
            
            if (rank == "A") {
                aces++;
            } else if (rank == "J" || rank == "Q" || rank == "K") {
                value += 10;
            } else if (int.TryParse(rank, out int val)) {
                value += val;
            }
        }
        
        for (int i = 0; i < aces; i++) {
            if (value + 11 <= 21) {
                value += 11;
            } else {
                value += 1;
            }
        }
        
        return value;
    }
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome());
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return null;
            return JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
        });
    }
}