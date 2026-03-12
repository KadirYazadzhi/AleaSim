# 🎲 DiceHub Game Design Document

> **Platform:** AleaSim Casino  
> **Version:** 1.0  
> **Status:** Draft

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Core Mechanic](#core-mechanic)
3. [Payout Formula](#payout-formula)
4. [Risk Levels](#risk-levels)
5. [Bet Modes — Manual and Auto-Bet](#bet-modes--manual-and-auto-bet)
6. [Paytable Examples](#paytable-examples)
7. [House Edge](#house-edge)
8. [Brain Integration](#brain-integration)
9. [Provably Fair Dice Roll](#provably-fair-dice-roll)
10. [Game History Display](#game-history-display)
11. [State Management](#state-management)
12. [UI Flow](#ui-flow)

---

## 🎯 Overview

DiceHub is AleaSim's fast-paced provably-fair dice game. It is designed for high-frequency, single-click play where the player selects a number range and win multiplier. The outcome of each roll is a number between 0.00 and 99.99 (two decimal precision), and the player wins if the result falls within their chosen range.

DiceHub is intentionally simple — no complex rules, no waiting for dealer animations. A game round completes in under a second, making it the fastest-cycle game on the platform.

---

## 🎯 Core Mechanic

### Roll Range

Each dice roll produces a result in the range **[0.00, 99.99]**, uniformly distributed, with 10,000 possible outcomes (0.00 through 99.99 in steps of 0.01).

### Win Condition Types

| Type | Description | Example |
|---|---|---|
| **Roll Over** | Result must be strictly greater than the target | Roll Over 55.00 → win if result > 55.00 |
| **Roll Under** | Result must be strictly less than the target | Roll Under 45.00 → win if result < 45.00 |

Players can toggle between Roll Over and Roll Under. The win probability and multiplier update in real-time as the target number is adjusted.

### Target Number Selection

The target number is set by:
1. **Slider:** Drag left/right to adjust the target (range: 0.00 – 99.99)
2. **Direct input:** Type a specific value into the target field
3. **Preset buttons:** Click risk level presets (Low, Medium, High, Extreme)

The UI enforces minimum win probability of **0.01%** (target = 99.98 for Roll Over, or 0.01 for Roll Under) and maximum win probability of **98.00%** to prevent near-certain outcomes with tiny payouts.

---

## 🧮 Payout Formula

The multiplier is calculated from the win probability and house edge:

```
win_probability = (winning_outcomes / 10000) × 100%

multiplier = (100 - house_edge_percent) / win_probability_percent
```

### Formula Example — Roll Over 55.00

```
Winning outcomes = 99.99 - 55.00 = 44.99 → 4499 outcomes out of 10000
Win probability  = 44.99%

Multiplier = (100 - 1) / 44.99
           = 99 / 44.99
           = 2.2005×  (displayed as 2.20×)

Payout on $10 bet = $10 × 2.20 = $22.00
Net win           = $22.00 - $10.00 = $12.00
```

### Formula Example — Roll Under 10.00

```
Winning outcomes = 10.00 → 1000 outcomes out of 10000
Win probability  = 10.00%

Multiplier = 99 / 10.00 = 9.90×

Payout on $10 bet = $10 × 9.90 = $99.00
Net win           = $89.00
```

### Formula Example — Roll Over 98.00

```
Winning outcomes = 99.99 - 98.00 = 1.99 → 199 outcomes
Win probability  = 1.99%

Multiplier = 99 / 1.99 = 49.75×

Payout on $100 bet = $4,975.00
Net win            = $4,875.00
```

> 🔒 **Maximum multiplier cap:** 9900× (corresponding to 0.01% win probability). Platform operators may set a lower cap per jurisdiction.

---

## ⚖️ Risk Levels

DiceHub provides four preset risk profiles for quick target selection. Each profile pre-fills the target and mode fields.

### Low Risk

| Setting | Value |
|---|---|
| Mode | Roll Over |
| Target | 47.00 |
| Win Probability | ~53% |
| Multiplier | ~1.87× |
| Description | Near 50/50 with modest multiplier. Ideal for casual play and auto-bet grinding. |

### Medium Risk

| Setting | Value |
|---|---|
| Mode | Roll Over |
| Target | 75.00 |
| Win Probability | ~25% |
| Multiplier | ~3.96× |
| Description | Balanced risk/reward. Popular for manual play with meaningful wins. |

### High Risk

| Setting | Value |
|---|---|
| Mode | Roll Over |
| Target | 90.00 |
| Win Probability | ~10% |
| Multiplier | ~9.90× |
| Description | Infrequent wins but strong payouts. Suitable for experienced players. |

### Extreme Risk

| Setting | Value |
|---|---|
| Mode | Roll Over |
| Target | 99.00 |
| Win Probability | ~1% |
| Multiplier | ~99×  |
| Description | Very rare wins, lottery-style thrill. Maximum bet limited by platform jackpot cap. |

---

## 🤖 Bet Modes — Manual and Auto-Bet

### Manual Mode

The standard mode. Player clicks [Roll] for each individual dice roll. No automatic behaviour.

### Auto-Bet Mode

Auto-Bet allows the player to configure a sequence of automatic rolls without manual intervention. It is designed for strategy testing and high-frequency play.

#### Auto-Bet Configuration Parameters

| Parameter | Description | Default |
|---|---|---|
| `Number of Bets` | Total rolls to execute (1–10,000, or ∞) | 100 |
| `Bet Amount` | Wager per roll | Same as last manual bet |
| `On Win: Increase Bet By` | % increase applied to bet after a win | 0% |
| `On Win: Reset Bet` | Reset to base bet after a win | Off |
| `On Loss: Increase Bet By` | % increase applied to bet after a loss (Martingale) | 0% |
| `On Loss: Reset Bet` | Reset to base bet after a loss | Off |
| `Stop on Win Above` | Halt auto-bet if session profit exceeds this value | Off |
| `Stop on Loss Above` | Halt auto-bet if session loss exceeds this value | Off |
| `Stop on Balance Below` | Halt auto-bet if balance drops below this value | Off |

#### Auto-Bet Safety Limits

- Single roll bet cannot exceed the platform maximum bet limit even in auto-bet mode
- Martingale (double-on-loss) will automatically halt if the next bet would exceed maximum bet
- Auto-bet pauses and requires player confirmation if the Martingale sequence would require >20× the original bet
- Auto-bet is suspended (not cancelled) if a Brain cooling phase is detected

#### Auto-Bet UI Controls

```
[▶ Start Auto-Bet]   → Begins rolling with configured parameters
[⏸ Pause]           → Suspends rolling, preserves remaining count
[⏹ Stop]            → Ends auto-bet, returns to manual mode
```

---

## 📋 Paytable Examples

The following table shows multipliers for common targets at 1% house edge:

| Mode | Target | Win Probability | Multiplier | Net Win on $10 |
|---|---|---|---|---|
| Roll Under | 5.00 | 5.00% | 19.80× | $188.00 |
| Roll Under | 10.00 | 10.00% | 9.90× | $89.00 |
| Roll Under | 25.00 | 25.00% | 3.96× | $29.60 |
| Roll Over | 47.00 | 53.00% | 1.87× | $8.70 |
| Roll Over | 50.00 | 49.99% | 1.98× | $9.80 |
| Roll Over | 75.00 | 25.00% | 3.96× | $29.60 |
| Roll Over | 90.00 | 10.00% | 9.90× | $89.00 |
| Roll Over | 95.00 | 5.00% | 19.80× | $188.00 |
| Roll Over | 99.00 | 1.00% | 99.00× | $980.00 |
| Roll Over | 99.90 | 0.10% | 990.00× | $9,890.00 |

---

## 📊 House Edge

DiceHub has a fixed, transparent house edge of **1%** applied uniformly to all bets regardless of target or risk level.

### House Edge Verification

```
For any bet of $X with win probability p:

  Expected payout = X × multiplier × p
                  = X × ((100 - 1) / p_percent) × (p_percent / 100)
                  = X × 99 / 100
                  = 0.99 × X

  House retains = X - 0.99X = 0.01X = 1% of every bet
```

This makes DiceHub's house edge identical regardless of whether the player chooses Roll Over 1.00 (near-certain win, tiny multiplier) or Roll Over 99.90 (rare win, huge multiplier).

### Comparison to Other AleaSim Games

| Game | House Edge |
|---|---|
| DiceHub | 1.00% |
| Blackjack (basic strategy) | ~0.50% |
| European Roulette | 2.70% |
| Baccarat (Banker) | 1.06% |
| Baccarat (Tie) | 14.36% |
| American Roulette | 5.26% |

---

## 🧠 Brain Integration

### Brain Role in DiceHub

DiceHub's single-click mechanic makes it the game most susceptible to rapid RTP drift. The Brain monitors session-level and platform-level RTP in real-time and applies throttling during cooling phases.

### Cooling Phase Throttling

During a cooling phase, the Brain **reduces the statistical probability of big wins** by adjusting the HMAC-derived result mapping:

```
Normal mode:   result is uniformly distributed [0.00, 99.99]

Cooling mode:  result is biased toward the "loss" side of the target:
  - For Roll Over 90.00: bias away from [90.01, 99.99] toward [00.00, 90.00]
  - Bias strength: up to 15% probability reduction on the win range
  - Net effect: effective win probability drops from 10% to ~8.5%
  - Effective house edge during cooling: up to ~15% temporarily
```

> ⚠️ **Regulatory Constraint:** Cooling adjustments are time-bounded. The Brain must return the observed session RTP to within ±0.3 percentage points of the declared 1% house edge (i.e., observed house edge must be between 0.7% and 1.3%) within a maximum of 500 consecutive rolls per session. Measurement is based on `(total_wagered - total_paid_out) / total_wagered` over the rolling 500-roll window.

### Heating Phase

During heating (player retention mode), the Brain biases results toward wins at low multipliers to provide positive feedback:

```
Heating mode: bias toward Roll Over win at current target
  - Only applied if current multiplier < 5× (to control RTP cost)
  - Creates a visible "win streak" sensation
  - Effective house edge during heating may drop to ~0.3%
```

### Brain Audit Log

Every Brain-influenced roll is logged with:
```json
{
  "roll_id": "uuid",
  "player_id": "uuid",
  "brain_mode": "COOLING",
  "declared_win_probability": 10.00,
  "adjusted_win_probability": 8.50,
  "actual_result": 45.23,
  "outcome": "LOSS",
  "session_rtp_at_roll": 1.023
}
```

---

## 🔐 Provably Fair Dice Roll

Each roll is independently verifiable using HMAC-SHA256.

### Roll Verification Flow

```
1. Server generates per-roll:
   - server_seed  (fresh 256-bit random value, new each roll in manual mode)
   - nonce        (incrementing integer, unique per server_seed)

2. Server publishes commitment before roll:
   commitment = HMAC-SHA256(key=server_seed, message=nonce)

3. Player provides client_seed (set once per session, changeable between rolls).

4. Roll result derived:
   roll_hash  = HMAC-SHA256(key=server_seed, message="{client_seed}:{nonce}")
   raw_int    = first_4_bytes_as_uint32(roll_hash)
   raw_result = raw_int mod 10000           // Range: 0–9999
   dice_result = raw_result / 100.0         // Range: 0.00–99.99

5. Win condition evaluated:
   Roll Over X: dice_result > X → WIN
   Roll Under X: dice_result < X → WIN

6. After roll, server reveals:
   - server_seed (when player changes client_seed or requests reveal)
   - All nonces used with that server_seed

7. Player verifies:
   a. For each nonce, recompute HMAC and extract result
   b. Confirm dice_result matches game history
   c. Recompute commitment and verify it matches hash shown before rolls
```

### Nonce System

The nonce increments with each roll under the same server_seed. The server_seed rotates when:
- Player manually changes their client_seed
- 1,000 rolls have been made with the current server_seed
- Player starts a new session

### Verification Endpoint

```
GET /api/verify/dice/{roll_id}
Response: {
  roll_id,
  commitment,
  server_seed,        // Only returned after seed rotation
  next_server_seed_commitment,
  client_seed,
  nonce,
  roll_hash,
  raw_int,
  dice_result,
  win_target,
  win_mode,
  outcome,
  payout,
  brain_mode         // Included for transparency; always in audit log
}
```

---

## 📈 Game History Display

DiceHub maintains a persistent in-session game history visible in the UI.

### History Strip (Above Roll Button)

A scrolling row of the last 50 results displayed as colour-coded bubbles:

```
[🟢 78.43] [🔴 23.11] [🟢 91.02] [🔴 45.88] [🟢 96.01] ...
```

- 🟢 Green = Win
- 🔴 Red = Loss
- Each bubble shows the dice result value
- Hovering a bubble shows: bet amount, multiplier, payout, timestamp

### Statistics Panel

```
┌──────────────────────────────────────────────┐
│  Session Statistics                          │
│  Total Rolls:     1,247                      │
│  Win Rate:        24.8% (expected: 25.0%)    │
│  Total Wagered:   $12,470.00                 │
│  Total Won:       $12,203.40                 │
│  Net:             −$266.60 (−2.14%)          │
│  Biggest Win:     $4,800.00 (Roll Over 98.0) │
│  Current Streak:  Loss ×3                    │
│  Longest Win Streak: 7                       │
│  Longest Loss Streak: 12                     │
└──────────────────────────────────────────────┘
```

### Full History Table (Expandable)

| # | Result | Mode | Target | Outcome | Multiplier | Bet | Payout |
|---|---|---|---|---|---|---|---|
| 1247 | 45.23 | Over | 90.00 | LOSS | 9.90× | $10.00 | −$10.00 |
| 1246 | 96.01 | Over | 90.00 | WIN | 9.90× | $10.00 | +$89.00 |
| … | … | … | … | … | … | … | … |

The table is paginated (50 rows per page). Full history exportable as CSV.

---

## 🗄️ State Management

DiceHub is stateless between rolls — each roll is independently self-contained. Session-level state is lightweight.

### Redis Key Schema

```
dice:session:{session_id}:server_seed         → string: current server seed (encrypted)
dice:session:{session_id}:server_seed_commit  → string: HMAC commitment for current seed
dice:session:{session_id}:client_seed         → string: current client seed
dice:session:{session_id}:nonce               → integer: current nonce counter
dice:session:{session_id}:history             → JSON array: last 1000 rolls
dice:session:{session_id}:autobet_config      → JSON: auto-bet parameters (if active)
dice:session:{session_id}:autobet_state       → JSON: auto-bet progress
dice:session:{session_id}:stats               → JSON: session statistics aggregate
```

### Roll Record Object

```json
{
  "roll_id": "uuid",
  "session_id": "uuid",
  "player_id": "uuid",
  "nonce": 142,
  "client_seed": "abc123",
  "dice_result": 45.23,
  "win_mode": "OVER",
  "win_target": 90.00,
  "outcome": "LOSS",
  "multiplier": 9.90,
  "bet_amount": 10.00,
  "payout": 0.00,
  "net": -10.00,
  "brain_mode": "NORMAL",
  "timestamp": "ISO8601"
}
```

### Auto-Bet State Object

```json
{
  "status": "RUNNING | PAUSED | STOPPED",
  "rolls_remaining": 857,
  "rolls_completed": 143,
  "base_bet": 10.00,
  "current_bet": 10.00,
  "session_profit": -125.00,
  "stop_conditions": {
    "win_above": null,
    "loss_above": 500.00,
    "balance_below": null
  },
  "on_win": { "action": "RESET", "increase_pct": 0 },
  "on_loss": { "action": "INCREASE", "increase_pct": 10 }
}
```

### TTL Policy

| State | TTL |
|---|---|
| Session seeds and nonce | 24 hours (or until explicit rotation) |
| Roll history in Redis | 6 hours (then archived to PostgreSQL) |
| Auto-bet config | 30 minutes after last roll |

---

## 🖥️ UI Flow

### Main Interface Layout

```
┌─────────────────────────────────────────────────┐
│  🎲 DiceHub                        [⚙️] [📊]   │
├─────────────────────────────────────────────────┤
│  History: [🟢78] [🔴23] [🟢91] [🔴45] [🟢96] │
├─────────────────────────────────────────────────┤
│                                                 │
│         RESULT: 45.23   ← animated number       │
│         ● ——————|———————————— ●                 │
│        0.00    45.23         99.99              │
│                                                 │
├─────────────────────────────────────────────────┤
│  [Roll Under ▼]   Target: [90.00]  [Roll Over ▲] │
│                                                 │
│  ◀═══════════════●════════════════▶            │
│  0.00           90.00          99.99           │
│                                                 │
│  Win Chance: 10.00%     Multiplier: 9.90×      │
├─────────────────────────────────────────────────┤
│  Bet Amount: [$10.00 ▲▼]   [½] [2×] [MAX]      │
│                                                 │
│  Risk: [Low] [Medium] [High] [Extreme]          │
│                                                 │
│         [🎲  ROLL  ]   [🤖 Auto-Bet]           │
├─────────────────────────────────────────────────┤
│  Balance: $1,234.56      Session: −$66.40       │
└─────────────────────────────────────────────────┘
```

### Roll Animation

```
1. Player clicks [ROLL]
2. Number display animates: rapidly cycles through values (30ms intervals)
3. Deceleration phase: slows to final result over ~400ms
4. Result locks in with a bounce effect
5. Win region highlighted in green / Loss region in red on slider
6. Win: 🎉 pulse animation, balance credit animation (+$89.00 floats up)
7. Loss: red flash, balance deduction animation (−$10.00 fades down)
8. History strip updates with new result bubble (slides in from right)
9. Ready for next roll: total time ~600ms
```

### Auto-Bet Panel (Expanded)

```
┌─────────────────────────────────────┐
│  🤖 Auto-Bet Configuration          │
│  Number of Bets: [100    ] [∞]      │
│  On Win:   [Reset ▼]  +[0   ]%      │
│  On Loss:  [Increase▼] +[10  ]%     │
│  Stop if win > [$     ]             │
│  Stop if loss > [$500  ]            │
│  Stop if balance < [$   ]           │
│                                     │
│     [▶ Start Auto-Bet]              │
└─────────────────────────────────────┘
```

### Provably Fair Panel

```
┌─────────────────────────────────────┐
│  🔐 Provably Fair                   │
│  Server Seed (next):                │
│  [hash: 8f3a...c2d1] [🔍 Verify]   │
│  Client Seed:                       │
│  [my-custom-seed    ] [✏️ Change]   │
│  Nonce: 142                         │
│  [📋 View Roll History & Verify]    │
└─────────────────────────────────────┘
```

---

## 🔗 Related Documents

- `GameDesign/Brain_RTP_System.md` — Full Brain architecture
- `GameDesign/ProvablyFair_Specification.md` — HMAC verification standard
- `GameDesign/SessionManagement.md` — Redis state lifecycle
