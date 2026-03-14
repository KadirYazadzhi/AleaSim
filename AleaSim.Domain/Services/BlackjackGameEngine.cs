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
        public bool IsSplitHand { get; set; }
    }

    public class BlackjackState {
        public List<BlackjackHand> PlayerHands { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public int ActiveHandIndex { get; set; } = 0; 
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
        public decimal TotalInitialBet { get; set; }
        public decimal TotalWin { get; set; } // Added for frontend reporting
        
        // Legacy fields for UI compatibility if needed, but we should update UI
        public List<string> PlayerHand => PlayerHands.Count > 0 ? PlayerHands[0].Cards : new();
        public List<string>? SplitHand => PlayerHands.Count > 1 ? PlayerHands[1].Cards : null;
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

            var state = new BlackjackState { TotalInitialBet = lastBet.Amount, Sequence = seq };
            var mainHand = new BlackjackHand { Bet = lastBet.Amount };
            state.PlayerHands.Add(mainHand);
            
            // Check for forced directives
            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, lastBet.Amount, repo);

            mainHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            mainHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
            state.Sequence = seq;

            if (CalculateHandValue(mainHand.Cards) == 21) {
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
            
            if (state.IsRoundOver) round.TotalWinAmount = CalculateWin(state);

            repo.SaveRound(round);
            repo.UpdateSession(session);
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

            var currentHand = state.PlayerHands[state.ActiveHandIndex];

            if (action.ToLower() == "double" && currentHand.Cards.Count == 2) {
                if (state.PlayerHands.Count > 1) throw new Exception("Double Down not allowed after Split.");
                
                int val = CalculateHandValue(currentHand.Cards);
                if (val != 10 && val != 11) throw new Exception("Only double on 10 or 11.");

                if (await VaultService.ProcessBetAsync(session.UserId, currentHand.Bet, repo)) {
                    repo.UpdateRtpStats(session.GameId, session.UserId, currentHand.Bet, 0); 
                    currentHand.IsDoubled = true;
                    currentHand.Bet *= 2;

                    int seq = state.Sequence;
                    currentHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                    state.Sequence = seq;
                    
                    await AdvanceOrFinish(session, state, repo, questService);
                } else throw new Exception("Insufficient funds");
            } 
            else if (action.ToLower() == "split" && state.PlayerHands.Count == 1 && currentHand.Cards.Count == 2) {
                string r1 = GetRank(currentHand.Cards[0]);
                string r2 = GetRank(currentHand.Cards[1]);
                
                if (r1 == r2 || (IsTenValue(r1) && IsTenValue(r2))) {
                    if (await VaultService.ProcessBetAsync(session.UserId, currentHand.Bet, repo)) {
                        repo.UpdateRtpStats(session.GameId, session.UserId, currentHand.Bet, 0);
                        
                        var secondHand = new BlackjackHand { 
                            Bet = currentHand.Bet, 
                            IsSplitHand = true,
                            Cards = new List<string> { currentHand.Cards[1] }
                        };
                        currentHand.Cards.RemoveAt(1);
                        state.PlayerHands.Add(secondHand);

                        int seq = state.Sequence;
                        currentHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                        secondHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                        state.Sequence = seq;
                    } else throw new Exception("Insufficient funds");
                }
            } 
            else if (action.ToLower() == "insurance" && state.DealerHand.Count == 2 && state.DealerHand[0].StartsWith("A") && !state.IsRoundOver) {
                if (state.PlayerHands.Count > 1) throw new Exception("No insurance after split.");
                decimal ins = currentHand.Bet / 2;
                if (await VaultService.ProcessBetAsync(session.UserId, ins, repo)) {
                    if (CalculateHandValue(state.DealerHand) == 21) {
                        decimal win = ins * 3;
                        repo.UpdateGamePoolBalance(session.GameId, -win);
                        await VaultService.ProcessWinAsync(session.UserId, win, repo);
                        repo.UpdateRtpStats(session.GameId, session.UserId, ins, win);
                    } else {
                        repo.UpdateRtpStats(session.GameId, session.UserId, ins, 0);
                    }
                }
            }
            else if (action.ToLower() == "hit") {
                int seq = state.Sequence;
                currentHand.Cards.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                state.Sequence = seq;
                if (CalculateHandValue(currentHand.Cards) >= 21) {
                    await AdvanceOrFinish(session, state, repo, questService);
                }
            } 
            else if (action.ToLower() == "stand") {
                currentHand.IsStand = true;
                await AdvanceOrFinish(session, state, repo, questService);
            }

            round.RandomResult = JsonSerializer.Serialize(state);
            if (state.IsRoundOver) round.TotalWinAmount = CalculateWin(state);
            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Action = action, State = state });
        });
    }

    private async Task AdvanceOrFinish(GameSession session, BlackjackState state, IGameRepository repo, IQuestService questService) {
        if (state.ActiveHandIndex < state.PlayerHands.Count - 1) {
            state.ActiveHandIndex++;
        } else {
            await FinishRoundAsync(session, state, repo, questService);
        }
    }

    private async Task FinishRoundAsync(GameSession session, BlackjackState state, IGameRepository repo, IQuestService questService) {
        state.IsRoundOver = true;
        int dVal = CalculateHandValue(state.DealerHand);
        bool anyHandAlive = state.PlayerHands.Any(h => CalculateHandValue(h.Cards) <= 21);

        if (anyHandAlive) {
            int seq = state.Sequence;
            while (dVal < 17) {
                state.DealerHand.Add(DrawCard(session.ServerSeed, session.ClientSeed, ref seq));
                dVal = CalculateHandValue(state.DealerHand);
            }
            state.Sequence = seq;
        }

        decimal totalWin = CalculateWin(state);
        state.TotalWin = totalWin;
        if (totalWin > 0) {
            repo.UpdateGamePoolBalance(session.GameId, -totalWin);
            await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
            repo.UpdateRtpStats(session.GameId, session.UserId, 0, totalWin);
            await questService.UpdateProgressAsync(session.UserId, "WinAmount", totalWin, repo, RealTimeService, VaultService);
        }
        BrainService.UpdateProfile(session.UserId, state.TotalInitialBet, totalWin, repo);
    }

    private decimal CalculateWin(BlackjackState state) {
        int dVal = CalculateHandValue(state.DealerHand);
        bool dBus = dVal > 21;
        bool dBJ = dVal == 21 && state.DealerHand.Count == 2;
        decimal totalWin = 0;

        foreach (var hand in state.PlayerHands) {
            int pVal = CalculateHandValue(hand.Cards);
            if (pVal > 21) continue; // Bust

            bool pBJ = pVal == 21 && hand.Cards.Count == 2 && !hand.IsSplitHand;

            if (pBJ) {
                if (dBJ) totalWin += hand.Bet; // Push
                else totalWin += hand.Bet * 2.5m; // 3:2
            } else {
                if (dBJ) continue; // Dealer BJ beats everything except Player BJ
                if (dBus || pVal > dVal) totalWin += hand.Bet * 2;
                else if (pVal == dVal) totalWin += hand.Bet;
            }
        }
        return totalWin;
    }

    private int CalculateHandValue(List<string> hand) {
        int val = 0; int aces = 0;
        foreach (var c in hand) {
            string r = GetRank(c);
            if (r == "A") aces++;
            else if (IsTenValue(r)) val += 10;
            else val += int.Parse(r);
        }
        for (int i = 0; i < aces; i++) val += (val + 11 <= 21) ? 11 : 1;
        return val;
    }

    private string GetRank(string card) => card.Substring(0, card.Length - 1);
    private bool IsTenValue(string r) => r == "10" || r == "J" || r == "Q" || r == "K";

    private string DrawCard(string serverSeed, string clientSeed, ref int seq) {
        seq++;
        int idx = RngService.GetNextInt(serverSeed, clientSeed, seq, 0, 52);
        string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        string[] suits = { "H", "D", "C", "S" };
        return ranks[idx % 13] + suits[idx / 13];
    }

    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await ExecuteScopedAsync(repo => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return Task.FromResult<object?>(null);
            return Task.FromResult<object?>(JsonSerializer.Deserialize<BlackjackState>(round.RandomResult));
        });
    }
}
