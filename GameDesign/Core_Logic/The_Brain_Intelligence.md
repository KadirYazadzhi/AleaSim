# 🧠 The Brain: Decision Intelligence

The Brain is the "invisible director" of AleaSim. Its objective is to optimize player engagement while maintaining strict house profitability.

---

## 1. Decision Inputs
The Brain intercept every spin request and analyzes:
*   **AvgSpinInterval:** Measures player excitement/boredom.
*   **LossStreak:** Tracks consecutive non-winning rounds.
*   **pRTP Delta:** Difference between current user luck and target RTP (95%).
*   **LTV (Lifetime Value):** Total player contribution.

---

## 2. Decision Logic (Hierarchy)

### Tier 1: System Overrides
*   **Global Shadow Mode:** If the Admin enables this, the Brain is bypassed, and pure RNG is used for all players.
*   **Admin Forced Directive:** Manual overrides (Force Win/Loss) for specific user IDs (expires in 10 mins).

### Tier 2: Retention Mechanisms
*   **The Hook:** Triggered on high loss streaks. Forces a 15x-25x win to deliver a dopamine "Sugar Hit".
*   **Near Miss:** Triggered when the Vault cannot afford a win. Constructs a "7-7-Lemon" visual to maintain high heart rates at zero cost.

### Tier 3: Adaptive Volatility (Flow State)
*   **Fast Play (< 2.5s):** User is in "The Zone". Volatility is boosted (fewer hits, much higher multipliers).
*   **Slow Play (> 10s):** User is losing interest. Volatility is lowered (frequent small "Popcorn Wins") to pull them back in.

---

## 3. Mathematical Continuity
*   **Unique Nonces:** Every attempt to match a Brain directive uses a new attempt offset in the HMAC-SHA256 RNG. This ensures that even if the Brain requests a re-roll, the outcome remains cryptographically fair and unique.
*   **Entropy Injection:** The Brain uses the `ClientSeed` provided by the player, ensuring the casino cannot predict the result before the player joins.

---

## 4. Full Decision Tree

Text-based flowchart of the Brain's complete evaluation order on every spin request:

```
START (Spin Request Received)
        │
        ▼
┌─────────────────────────────┐
│  1. Admin Override Check    │  Is there an active ForceDirective for this userId?
└─────────────┬───────────────┘
              │
      Directive present?
       ┌──────┴──────┐
      YES            NO
       │              │
       ▼              ▼
 ┌───────────┐  ┌──────────────────────────┐
 │  FORCE    │  │  2. Global Shadow Mode?  │  Has admin enabled Shadow Mode globally?
 │ DIRECTIVE │  └────────────┬─────────────┘
 │ (Win/Loss)│               │
 └─────┬─────┘       Shadow Mode ON?
       │              ┌──────┴──────┐
       │             YES            NO
       │              │              │
       │              ▼              ▼
       │        ┌──────────┐  ┌─────────────────────────┐
       │        │ PURE RNG │  │  3. pRTP Delta Check    │
       │        │ (bypass) │  │  Is user RTP too far     │
       │        └──────────┘  │  above or below target? │
       │                      └────────────┬────────────┘
       │                                   │
       │                    pRTP delta outside tolerance?
       │                     ┌─────────────┴─────────────┐
       │                  TOO HIGH                    TOO LOW
       │                  (user winning               (user losing
       │                   too much)                   too much)
       │                     │                           │
       │                     ▼                           ▼
       │             ┌───────────────┐         ┌────────────────┐
       │             │ SUPPRESS mode │         │  THE HOOK mode │
       │             │ (reduce wins, │         │  Force 15x-25x │
       │             │  near-misses) │         │  win output    │
       │             └───────┬───────┘         └───────┬────────┘
       │                     │                         │
       │                     └──────────┬──────────────┘
       │                                │
       │                                ▼
       │                   ┌────────────────────────┐
       │                   │  4. Loss Streak Check  │  LossStreak ≥ threshold?
       │                   └────────────┬───────────┘
       │                                │
       │                   ┌────────────┴────────────┐
       │                 YES (≥5)                    NO
       │                   │                          │
       │                   ▼                          ▼
       │           ┌───────────────┐     ┌───────────────────────┐
       │           │  SAVER BOMB / │     │  5. Flow State Check  │
       │           │  POPCORN WIN  │     │  AvgSpinInterval < 2.5s?
       │           └───────┬───────┘     └──────────────┬────────┘
       │                   │                             │
       │                   │              ┌──────────────┴──────────────┐
       │                   │           FAST (< 2.5s)              SLOW (> 10s)
       │                   │              │                             │
       │                   │              ▼                             ▼
       │                   │     ┌────────────────┐          ┌──────────────────┐
       │                   │     │ FLOW STATE:    │          │ RE-ENGAGE MODE:  │
       │                   │     │ Boost volatility│         │ Lower volatility  │
       │                   │     │ fewer hits,    │          │ frequent small   │
       │                   │     │ bigger prizes  │          │ "popcorn" wins   │
       │                   │     └───────┬────────┘          └────────┬─────────┘
       │                   │             │                             │
       └───────────────────┴─────────────┴─────────────────────────────┘
                                         │
                                         ▼
                              ┌──────────────────────┐
                              │  6. Normal RNG Path  │  Generate outcome via
                              │  (adjusted weights)  │  HMAC-SHA256 + ClientSeed
                              └──────────────────────┘
                                         │
                                         ▼
                              ┌──────────────────────┐
                              │  Vault Authorization │  Check accruedPool
                              │  (win allowed?)      │  and GlobalPool
                              └──────────────────────┘
                                         │
                                         ▼
                                  Return Spin Result
```

---

## 5. DecisionType Enum Values

| Enum Value | Integer | Effect on Game Engine |
| :--- | :---: | :--- |
| `NormalRng` | 0 | Standard RNG path; no Brain intervention |
| `ForceWin` | 1 | Engine re-rolls until a winning outcome ≥ threshold is found |
| `ForceLoss` | 2 | Engine re-rolls until a non-winning outcome is found |
| `ForceNearMiss` | 3 | Engine constructs a near-miss visual (e.g., 7-7-Lemon) at zero cost |
| `SuppressWin` | 4 | Win multipliers are capped at `MaxSuppressedMultiplier` (default 2x) |
| `BoostVolatility` | 5 | Win probability reduced but available multipliers increased 3–5x |
| `LowerVolatility` | 6 | Win probability increased; multipliers capped at 5x |
| `SaverBomb` | 7 | Fruit Blast only: forces a TNT bomb on the next cascade drop |
| `NuclearBomb` | 8 | Fruit Blast only: forces a Nuclear bomb trigger |
| `HookWin` | 9 | Forces a win in the 15x–25x range (dopamine retention hit) |
| `ShadowBypass` | 10 | Full bypass — pure RNG, Brain result discarded |
| `AdminOverride` | 11 | Admin-set manual directive; expires after 10 minutes or 1 use |

---

## 6. LTV Calculation Formula

**LTV (Lifetime Value)** measures the net profit a player generates over their lifetime on the platform.

```
LTV = TotalWagered × HouseEdge − PromoCost − SupportCost
```

| Variable | Source | Description |
| :--- | :--- | :--- |
| `TotalWagered` | `RTPStatistics.TotalWagered` | Sum of all bets placed by the player (all time) |
| `HouseEdge` | Platform config (default 4%) | The fixed percentage retained per bet before payouts |
| `PromoCost` | `Transactions` WHERE `Type = Bonus` | Total value of all bonuses, free spins, and faucet grants |
| `SupportCost` | CRM system estimate | Estimated cost of customer support interactions |

**Example:**
```
TotalWagered = $10,000
HouseEdge    = 4%  → $400 gross margin
PromoCost    = $80 (bonuses claimed)
SupportCost  = $15 (estimated)

LTV = $400 − $80 − $15 = $305
```

The Brain uses LTV thresholds to decide how aggressively to retain a player: high-LTV players receive more generous Hook Wins; low-LTV new players receive standard volatility to evaluate their spending pattern.

---

## 7. Churning Risk Score Formula

The Brain calculates a **Churn Risk Score** (0.0 – 1.0) on every spin to predict whether a player is about to leave.

```
ChurnRisk = (w1 × NormLossStreak) + (w2 × SpinIntervalDelta) + (w3 × NormSessionLength)
```

| Variable | Calculation | Weight (`w`) |
| :--- | :--- | :---: |
| `NormLossStreak` | `LossStreak / MaxExpectedStreak` (capped at 1.0) | 0.45 |
| `SpinIntervalDelta` | `(CurrentAvgInterval − BaselineInterval) / BaselineInterval` (capped at 1.0) | 0.35 |
| `NormSessionLength` | `SessionMinutes / MaxEngagementWindow` (60 min default, capped at 1.0) | 0.20 |

**Thresholds:**
| Score Range | Risk Level | Brain Action |
| :--- | :--- | :--- |
| 0.0 – 0.35 | Low | No intervention |
| 0.36 – 0.60 | Medium | Lower volatility (popcorn wins) |
| 0.61 – 0.80 | High | Trigger Hook Win |
| 0.81 – 1.0 | Critical | Force Near-Miss + Hook Win sequence |

---

## 8. Complete Session Example Walkthrough

**Scenario:** Player "Alice" logs in, plays Clover Chase, hits a loss streak, triggers Brain intervention.

### Step 1 — Player Login
- Alice authenticates. JWT issued. Session record created in DB.
- Brain loads Alice's `RTPStatistics`: `TotalWagered = $4,200`, `TotalWon = $3,990`, `LossStreak = 0`.
- pRTP delta: `($3,990 / $4,200) = 95.0%` — exactly at target. No intervention needed.
- DecisionType assigned: `NormalRng`.

### Step 2 — First Spin ($1.00 bet)
- Alice spins. RNG generates outcome. Vault deducts $1.00 from balance.
- Result: 3× Lemon → 0.4x win = $0.40. Net −$0.60.
- `LossStreak` stays 0 (a win occurred, even if small).
- Brain logs: `AvgSpinInterval = 3.2s`. ChurnRisk = 0.02 (Low).

### Step 3 — Spins 2–7 (All Losses)
- Six consecutive non-winning spins. Each costs $1.00.
- After spin 7: `LossStreak = 6`, `TotalWagered += $6`, `TotalWon += $0`.
- pRTP drops: `$3,990 / $4,206 = 94.86%` — within tolerance.
- `AvgSpinInterval` slows to 5.8s (player hesitating).
- `SpinIntervalDelta = (5.8 − 3.2) / 3.2 = 0.81`.
- `NormLossStreak = LossStreak / MaxExpectedStreak = 6 / 10 = 0.60` (MaxExpectedStreak is 10 by default config).
- `ChurnRisk = (0.45 × 0.60) + (0.35 × 0.81) + (0.20 × 0.03) = 0.27 + 0.28 + 0.01 = **0.56** (Medium)`.
- Brain sets DecisionType: `LowerVolatility` (popcorn wins to re-engage).

### Step 4 — Spin 8 (Brain Intervenes — Popcorn Win)
- Brain directive: `LowerVolatility`. Engine biases RNG toward small wins.
- Result: 3× Cherry → 0.4x = $0.40. Not a big win, but breaks the streak.
- `LossStreak` resets to 0. Player sees a win; continues playing.
- ChurnRisk drops to 0.12 (Low). Brain returns to `NormalRng`.

### Step 5 — Spins 9–14 (Another Loss Streak)
- `LossStreak` climbs to 8. `AvgSpinInterval = 7.1s`.
- `ChurnRisk = (0.45 × 0.8) + (0.35 × 1.0) + (0.20 × 0.15) = 0.36 + 0.35 + 0.03 = **0.74** (High)`.
- pRTP now at 93.1% — below target. Both pRTP check AND ChurnRisk check fire.
- Brain sets DecisionType: `HookWin` (override).

### Step 6 — Spin 15 (Hook Win Triggered)
- Brain directive: `HookWin`. Engine re-rolls until a 15x–25x outcome is found.
- Result: 5× Seven (75x) lands after 3 re-roll attempts. Vault confirms `accruedPool` can cover $75.
- Alice wins $75.00 on a $1.00 bet. Huge dopamine hit.
- `LossStreak = 0`. pRTP jumps to 96.8% — back above target.
- Alice's session length increases by 12 more minutes. LTV contribution maintained.

### Session Summary
| Metric | Value |
| :--- | :--- |
| Total spins this session | 15 |
| Total wagered | $15.00 |
| Total won | $75.80 |
| Net player P&L | +$60.80 |
| Brain interventions | 2 (LowerVolatility + HookWin) |
| Final pRTP (session) | 505% (high — Hook Win corrects over next sessions) |
