# 🎴 Baccarat Game Design Document

> **Platform:** AleaSim Casino  
> **Version:** 1.0  
> **Status:** Draft

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Punto Banco Rules](#punto-banco-rules)
3. [Card Values](#card-values)
4. [Drawing Rules — Third Card Logic](#drawing-rules--third-card-logic)
5. [Paytable](#paytable)
6. [House Edge](#house-edge)
7. [Commission on Banker Wins](#commission-on-banker-wins)
8. [Brain RTP Management](#brain-rtp-management)
9. [Provably Fair Shoe Shuffle](#provably-fair-shoe-shuffle)
10. [State Management](#state-management)
11. [UI Flow](#ui-flow)

---

## 🎯 Overview

Baccarat (Punto Banco variant) is one of the simplest and most popular table games in AleaSim. Players do not make strategic decisions during play — they bet on one of three outcomes (Player, Banker, or Tie) before cards are dealt. The dealing and drawing rules are fully deterministic, making Baccarat ideal for provably-fair verification.

AleaSim implements standard 8-deck shoe Baccarat with a 5% commission on Banker wins, matching the rules found in major regulated casinos worldwide.

---

## 🃏 Punto Banco Rules

Baccarat is dealt from an 8-deck shoe (416 cards). Each round (called a **coup**) deals two hands:

- **Punto (Player):** The hand bet on by players choosing "Player"
- **Banco (Banker):** The hand that acts as the house

Neither hand belongs to a specific person. These are just two dealt positions on the table.

### Objective

The hand closest to a total of **9** wins. Values above 9 wrap around (i.e., 10 is 0, 15 is 5).

### Betting Options

| Bet | Wins When |
|---|---|
| Player | Player hand total > Banker hand total |
| Banker | Banker hand total > Player hand total |
| Tie | Both hands have equal totals |

Players may place multiple bet types simultaneously.

> 📌 **AleaSim variant rule:** By default, Player and Banker bets **lose** on a Tie. This is an AleaSim-specific configuration — in many traditional Punto Banco implementations, Player and Banker bets **push** (are returned) on a Tie. Operators may enable the push-on-tie rule via the `baccarat.tie_push_enabled` configuration flag.

---

## 🔢 Card Values

| Card | Value |
|---|---|
| Ace | 1 |
| 2 – 9 | Face value |
| 10, Jack, Queen, King | 0 |

### Hand Total Calculation

All hand totals are taken **modulo 10**. Only the units digit matters.

| Cards | Raw Total | Baccarat Total |
|---|---|---|
| 7 + 6 | 13 | 3 |
| K + 9 | 19 | 9 |
| 8 + 8 | 16 | 6 |
| A + 9 | 10 | 0 |
| 5 + 4 | 9 | 9 (Natural) |
| A + A | 2 | 2 |

### Natural Win

If either hand totals **8 or 9** after the initial two cards, it is a **Natural**. No further cards are drawn and the higher natural wins (or ties if equal).

---

## 🗺️ Drawing Rules — Third Card Logic

The third card rules are fixed and apply in strict order. There is no player decision.

### Player Drawing Rule

| Player Two-Card Total | Action |
|---|---|
| 0 – 5 | Draw a third card |
| 6 – 7 | Stand |
| 8 – 9 | Natural — No draw |

### Banker Drawing Rule

The Banker's action depends on the Banker's two-card total **and** the Player's third card (if drawn).

#### If Player Did NOT Draw a Third Card:

| Banker Total | Action |
|---|---|
| 0 – 5 | Draw |
| 6 – 7 | Stand |
| 8 – 9 | Natural — No draw |

#### If Player DID Draw a Third Card:

| Banker Total | Draw if Player's 3rd card is… | Stand if Player's 3rd card is… |
|---|---|---|
| 0, 1, 2 | Always draws | — |
| 3 | 0, 1, 2, 3, 4, 5, 6, 7, 9 | 8 |
| 4 | 2, 3, 4, 5, 6, 7 | 0, 1, 8, 9 |
| 5 | 4, 5, 6, 7 | 0, 1, 2, 3, 8, 9 |
| 6 | 6, 7 | 0, 1, 2, 3, 4, 5, 8, 9 |
| 7 | Always stands | — |
| 8, 9 | Natural — No draw | — |

> 💡 These rules are codified in the `BaccaratEngine.DetermineThirdCard()` method and are not configurable by operators.

---

## 💰 Paytable

| Bet | Payout | Commission |
|---|---|---|
| Player wins | 1:1 | None |
| Banker wins | 1:1 | −5% of winnings |
| Tie wins | 8:1 | None |
| Player loses | −1× bet | — |
| Banker loses | −1× bet | — |
| Tie (if Player/Banker bet) | Bet lost (default) | — |

### Banker Win Example

```
Bet: $100 on Banker
Banker wins → Gross payout: $100
Commission:  $100 × 5% = $5
Net payout:  $95
```

### Tie Bet Detail

The Tie bet pays 8:1, but some casinos offer 9:1. AleaSim uses **8:1** (standard). The higher 9:1 variant can be enabled per-operator through configuration.

---

## 📊 House Edge

| Bet | House Edge | Notes |
|---|---|---|
| Banker | ~1.06% | After 5% commission |
| Player | ~1.24% | No commission |
| Tie (8:1) | ~14.4% | Very high — not recommended |
| Tie (9:1) | ~4.8% | Optional variant |

### House Edge Derivation

```
Banker bet:
  P(Banker wins)  = 45.86%
  P(Player wins)  = 44.62%
  P(Tie)          = 9.52%

Expected value per $1 Banker bet:
  = (0.4586 × 0.95) − (0.4462 × 1) + (0.0952 × 0)
  = 0.43567 − 0.44620
  = −0.01053  →  House edge ≈ 1.06%

Player bet:
  Expected value per $1:
  = (0.4462 × 1) − (0.4586 × 1)
  = −0.0124  →  House edge ≈ 1.24%

Tie bet (8:1):
  = (0.0952 × 8) − (0.9048 × 1)
  = 0.7616 − 0.9048
  = −0.1432  →  House edge ≈ 14.36%
```

---

## 🏦 Commission on Banker Wins

The 5% commission compensates for the Banker hand's statistical advantage. Without commission, the Banker bet would have a player edge of approximately +1.2%.

### Commission Tracking

AleaSim tracks accumulated commission in two ways:

1. **Real-time deduction:** Commission deducted from each Banker win immediately.
2. **Running tally mode** *(optional operator config)*: Commission accumulated across a shoe and settled at the end or when the player leaves the table. Stored in Redis under `baccarat:session:{id}:commission_owed`.

---

## 🧠 Brain RTP Management

The Brain manages Baccarat RTP through **shoe composition weighting** at shuffle time. Unlike Blackjack, Baccarat has no player strategic decisions, making the Brain's influence purely statistical.

### Influence Mechanism

| Component | Brain Action |
|---|---|
| Shoe selection | Choose from pre-validated shoe compositions within ±0.2% RTP variance |
| Tie frequency | Marginally adjustable via shoe composition (10 vs King-heavy shoes) |
| Streak management | Brain can influence tendency toward Banker or Player streaks |

### RTP Modes

| Mode | Description |
|---|---|
| `NORMAL` | Fully uniform shoe selection |
| `COOLING` | Slightly favour Banker wins (offset by commission) |
| `HEATING` | Slightly favour Player wins (or more Ties for visual excitement) |

### Constraints

- All shoe compositions must produce a Banker house edge of 1.06% ± 0.2%
- Brain cannot retroactively modify shoe contents after the HMAC commitment is issued
- Every active shoe is logged to the audit trail in PostgreSQL

---

## 🔐 Provably Fair Shoe Shuffle

AleaSim uses HMAC-SHA256 to commit to a full 8-deck shoe (416 cards) before any cards are dealt in a round.

### Verification Flow

```
1. Server generates:
   - server_seed  (random 256-bit secret)
   - shoe_data    (ordered array of 416 cards)

2. Server publishes commitment BEFORE first card is dealt:
   commitment = HMAC-SHA256(key=server_seed, message=shoe_data_json)

3. Player optionally provides client_seed (default: random UUID).

4. Final shoe order derived:
   shuffle_seed = HMAC-SHA256(key=server_seed, message=client_seed)
   final_shoe   = Fisher-Yates(shoe_data, shuffle_seed)

5. After shoe completion or player request, server reveals:
   - server_seed
   - original shoe_data
   - client_seed used

6. Player verifies:
   a. Recompute commitment → must match published hash
   b. Recompute Fisher-Yates → must match game history card sequence
```

### Shoe Cut Card

At shuffle time, a cut card is placed at a random position between card 260–330 (out of 416). When the cut card is reached, the current coup completes and the shoe is reshuffled for the next round. The cut card position is included in the shoe_data commitment.

### Verification Endpoint

```
GET /api/verify/baccarat/{session_id}
Response: {
  commitment,
  server_seed,
  client_seed,
  shoe_data,
  cut_card_position,
  coup_history: [{ coup_number, player_cards, banker_cards, result }]
}
```

---

## 🗄️ State Management

### Redis Key Schema

```
baccarat:session:{session_id}:state          → JSON: current coup state
baccarat:session:{session_id}:shoe           → JSON: remaining shoe array
baccarat:session:{session_id}:shoe_index     → integer: current position in shoe
baccarat:session:{session_id}:commitment     → string: HMAC-SHA256 hash
baccarat:session:{session_id}:commission     → decimal: running commission owed (if tally mode)
baccarat:session:{session_id}:coup_history   → JSON array: all coups in current shoe
```

### Coup State Object

```json
{
  "session_id": "uuid",
  "player_id": "uuid",
  "coup_number": 14,
  "status": "BETTING | DEALING | RESOLVED",
  "bets": {
    "player_bet": 25.00,
    "banker_bet": 0.00,
    "tie_bet": 5.00
  },
  "player_hand": {
    "cards": [{"rank": "7", "suit": "DIAMONDS"}, {"rank": "K", "suit": "SPADES"}],
    "total": 7
  },
  "banker_hand": {
    "cards": [{"rank": "4", "suit": "CLUBS"}, {"rank": "5", "suit": "HEARTS"}],
    "total": 9
  },
  "result": "BANKER_NATURAL",
  "payout": -25.00,
  "shoe_index": 128,
  "created_at": "ISO8601"
}
```

### TTL Policy

| State | TTL |
|---|---|
| Active coup (betting open) | 120 seconds |
| Coup in progress (dealing) | 60 seconds |
| Between coups | 600 seconds |

---

## 🖥️ UI Flow

### Bet Phase

```
[Betting Timer: 20 seconds countdown]
  ↓
Player clicks chip denominations onto bet circles:
  [PLAYER] [TIE] [BANKER]
  ↓
Optional: Side bets (Pairs, Dragon Bonus if enabled)
  ↓
Timer expires or player clicks [Deal] → Bet locked
```

### Deal Animation

```
Card 1 → Player (face up)
Card 2 → Banker (face up)
Card 3 → Player (face up)
Card 4 → Banker (face up)
  ↓
[If Natural 8 or 9 → "NATURAL" banner displayed → Skip third card]
  ↓
[Third card drawn for Player if applicable → animated deal]
[Third card drawn for Banker per drawing rules → animated deal]
```

### Road Maps

Baccarat UI includes the four traditional road displays:

| Road | Description |
|---|---|
| **Big Road** | Main outcome grid (B/P/T, column-based) |
| **Big Eye Boy** | Pattern regularity indicator |
| **Small Road** | Two-step pattern indicator |
| **Cockroach Pig** | Three-step pattern indicator |

Road maps update in real-time after each coup. Historical data is sourced from the coup_history Redis key.

### Resolution Display

```
Result banner (e.g., "BANKER WINS — 9 vs 7")
  ↓
Win/Loss animation per bet circle
  ↓
Commission notice if Banker win (e.g., "−$1.25 commission")
  ↓
Balance updated
  ↓
[Next Coup] button or auto-advance after 3 seconds
```

### Statistics Panel

```
Session Stats:
  Player wins: 34%  |  Banker wins: 46%  |  Ties: 8%
  Current streak: Banker ×4
  Cards remaining in shoe: 152 / 416
```

---

## 🔗 Related Documents

- `GameDesign/Brain_RTP_System.md` — Full Brain architecture
- `GameDesign/ProvablyFair_Specification.md` — HMAC verification standard
- `GameDesign/SessionManagement.md` — Redis state lifecycle
