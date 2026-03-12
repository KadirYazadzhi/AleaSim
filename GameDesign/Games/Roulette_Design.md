# 🎡 Roulette Game Design Document

> **Platform:** AleaSim Casino  
> **Version:** 1.0  
> **Status:** Draft

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Variants](#variants)
3. [Wheel Layout](#wheel-layout)
4. [Inside Bets](#inside-bets)
5. [Outside Bets](#outside-bets)
6. [Full Payout Table](#full-payout-table)
7. [House Edge](#house-edge)
8. [Brain Integration](#brain-integration)
9. [Provably Fair Spin](#provably-fair-spin)
10. [State Management](#state-management)
11. [UI — Canvas Wheel Animation](#ui--canvas-wheel-animation)

---

## 🎯 Overview

Roulette is one of AleaSim's core table games, available in European (single-zero) and American (double-zero) variants. Players place bets on a numbered grid corresponding to pockets on a spinning wheel. A ball is released onto the spinning wheel and lands in one numbered pocket, determining all winning and losing bets.

AleaSim's Roulette uses cryptographically fair wheel spin outcomes via HMAC-SHA256, with a canvas-rendered animated wheel that plays the full spin animation before revealing the result.

---

## 🎰 Variants

| Feature | European | American |
|---|---|---|
| Pockets | 37 (0–36) | 38 (0, 00, 1–36) |
| Zeros | Single (0) | Double (0 and 00) |
| House Edge | 2.70% | 5.26% |
| Five Number Bet | ❌ Not available | ✅ Available |
| La Partage Rule | Optional (halves loss on even-money bets hitting 0) | ❌ Not available |
| En Prison Rule | Optional (imprisons even-money bets on 0 for next spin) | ❌ Not available |

> 📌 La Partage reduces the European house edge on even-money bets from 2.70% to **1.35%** when enabled.

---

## 🔵 Wheel Layout

### European Wheel Sequence (0–36)

```
0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8,
23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12,
35, 3, 26
```

### American Wheel Sequence (0–36 + 00)

```
0, 28, 9, 26, 30, 11, 7, 20, 32, 17, 5, 22, 34, 15, 3, 24, 36,
13, 1, 00, 27, 10, 25, 29, 12, 8, 19, 31, 18, 6, 21, 33, 16, 4,
23, 35, 14, 2
```

### Number Colours

| Colour | Numbers |
|---|---|
| Red | 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 |
| Black | 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 |
| Green | 0, 00 |

---

## 🎯 Inside Bets

Inside bets are placed on specific numbers or small groups of numbers on the betting grid.

### Straight Up

A bet on a single number, including 0 (and 00 in American).

| Property | Value |
|---|---|
| Coverage | 1 number |
| Payout | 35:1 |
| Win probability (European) | 1/37 = 2.70% |
| Win probability (American) | 1/38 = 2.63% |

### Split

A bet on two adjacent numbers on the grid (horizontal or vertical neighbour).

| Property | Value |
|---|---|
| Coverage | 2 numbers |
| Payout | 17:1 |
| Win probability (European) | 2/37 = 5.41% |

### Street

A bet on three consecutive numbers in a horizontal row on the grid.

| Property | Value |
|---|---|
| Coverage | 3 numbers |
| Payout | 11:1 |
| Example rows | 1-2-3, 4-5-6, ..., 34-35-36 |
| Win probability (European) | 3/37 = 8.11% |

### Corner (Square)

A bet on four numbers forming a square on the grid.

| Property | Value |
|---|---|
| Coverage | 4 numbers |
| Payout | 8:1 |
| Example | 1-2-4-5 |
| Win probability (European) | 4/37 = 10.81% |

### Six Line (Double Street)

A bet on two adjacent rows of three numbers (six numbers total).

| Property | Value |
|---|---|
| Coverage | 6 numbers |
| Payout | 5:1 |
| Example | 1-2-3-4-5-6 |
| Win probability (European) | 6/37 = 16.22% |

### Five Number Bet (American Only)

A bet covering 0, 00, 1, 2, and 3. Only available on the American wheel layout.

| Property | Value |
|---|---|
| Coverage | 5 numbers (0, 00, 1, 2, 3) |
| Payout | 6:1 |
| Win probability | 5/38 = 13.16% |
| House edge | **7.89%** — the worst bet on the American wheel |

> ⚠️ The Five Number Bet has a significantly higher house edge than all other Roulette bets due to the payout not being adjusted for the extra zero. It is displayed in the UI with a clear house-edge warning tooltip.

### Trio (European Only)

A three-number bet that includes the zero. Only certain combinations are valid.

| Valid Trios |
|---|
| 0-1-2 |
| 0-2-3 |

| Property | Value |
|---|---|
| Payout | 11:1 |
| Win probability (European) | 3/37 = 8.11% |

---

## 🟢 Outside Bets

Outside bets cover large groups of numbers and offer closer to even-money payouts.

### Red / Black

| Property | Value |
|---|---|
| Coverage | 18 red or 18 black numbers |
| Payout | 1:1 |
| Win probability (European) | 18/37 = 48.65% |
| Notes | 0 (and 00) are neither red nor black — both lose |

### Odd / Even

| Property | Value |
|---|---|
| Coverage | 18 odd or 18 even numbers (0 and 00 excluded) |
| Payout | 1:1 |
| Win probability (European) | 18/37 = 48.65% |

### Low (1–18) / High (19–36)

| Property | Value |
|---|---|
| Coverage | Numbers 1–18 or 19–36 |
| Payout | 1:1 |
| Win probability (European) | 18/37 = 48.65% |

### Dozens

| Property | Value |
|---|---|
| Coverage | 1st 12 (1–12), 2nd 12 (13–24), 3rd 12 (25–36) |
| Payout | 2:1 |
| Win probability (European) | 12/37 = 32.43% |

### Columns

| Property | Value |
|---|---|
| Coverage | Column 1 (1,4,7…34), Column 2 (2,5,8…35), Column 3 (3,6,9…36) |
| Payout | 2:1 |
| Win probability (European) | 12/37 = 32.43% |
| Notes | 0 and 00 are not in any column — all column bets lose |

---

## 💰 Full Payout Table

| Bet Type | Numbers Covered | Payout | European Win % | American Win % |
|---|---|---|---|---|
| Straight Up | 1 | 35:1 | 2.70% | 2.63% |
| Split | 2 | 17:1 | 5.41% | 5.26% |
| Trio | 3 | 11:1 | 8.11% | N/A |
| Street | 3 | 11:1 | 8.11% | 7.89% |
| Corner | 4 | 8:1 | 10.81% | 10.53% |
| Five Number | 5 | 6:1 | N/A | 13.16% |
| Six Line | 6 | 5:1 | 16.22% | 15.79% |
| Column | 12 | 2:1 | 32.43% | 31.58% |
| Dozen | 12 | 2:1 | 32.43% | 31.58% |
| Red/Black | 18 | 1:1 | 48.65% | 47.37% |
| Odd/Even | 18 | 1:1 | 48.65% | 47.37% |
| Low/High | 18 | 1:1 | 48.65% | 47.37% |

---

## 📊 House Edge

### European Roulette

```
House edge = 1 / 37 = 2.7027...%

Example — Straight Up bet:
  Win: +35 × (1/37) = +0.9459
  Loss: −1 × (36/37) = −0.9730
  EV = −0.0270  →  House edge = 2.70%

This is uniform across ALL bet types on the European wheel.
```

### American Roulette

```
House edge = 2 / 38 = 5.2632...%

Example — Straight Up bet:
  Win: +35 × (1/38) = +0.9211
  Loss: −1 × (37/38) = −0.9737
  EV = −0.0526  →  House edge = 5.26%

Exception: Five Number Bet = 7.89% (structural flaw in payout rate)
```

### La Partage Effect (European, Even-Money Bets)

```
With La Partage:
  If ball lands on 0, half the even-money bet is returned.
  House edge on Red/Black/Odd/Even/Low/High = 2.70% / 2 = 1.35%
```

---

## 🧠 Brain Integration

The Brain controls Roulette outcomes through **wheel sector selection** — choosing which numbered pocket the ball lands in. Since each spin is an independent event, the Brain can directly influence individual outcomes within defined statistical bounds.

### Mechanism: Sector-Weighted Spin

```
1. Brain evaluates current session RTP position.
2. Brain selects a weighted probability distribution over all 37/38 pockets.
3. Weighted distribution is combined with the provably-fair HMAC output:
   - HMAC output produces a raw pocket index (uniform distribution)
   - Brain weight remap function adjusts probability toward target RTP
4. Final pocket is determined from the remapped value.
5. The weight map used is embedded in the HMAC commitment for auditability.
```

### Brain RTP Modes — Roulette

| Mode | Behaviour |
|---|---|
| `NORMAL` | Uniform 1/37 (or 1/38) probability per pocket |
| `COOLING` | Slight bias away from heavily-bet numbers on the current layout |
| `HEATING` | Slight bias toward moderately-bet numbers (creates wins, increases retention) |

### Constraints

- Maximum weight deviation from uniform: ±1.5× per pocket
- Cumulative house edge must remain within declared rate ± 0.5%
- Weight map included in HMAC commitment and auditable post-spin

---

## 🔐 Provably Fair Spin

Each spin is individually verifiable using HMAC-SHA256.

### Spin Verification Flow

```
1. Server generates per-spin:
   - server_seed  (fresh 256-bit random value per spin)
   - weight_map   (Brain's probability weights, if in non-NORMAL mode)

2. Server publishes commitment before betting opens:
   commitment = HMAC-SHA256(key=server_seed, message=weight_map_json)

3. Player provides client_seed (or accepts random UUID default).

4. Spin result derived:
   spin_hash   = HMAC-SHA256(key=server_seed, message=client_seed)
   raw_index   = bytes_to_uint(spin_hash) mod (37 or 38)
   final_pocket = weight_remap(raw_index, weight_map)

5. After spin resolves, server reveals:
   - server_seed
   - weight_map
   - client_seed

6. Player verifies:
   a. Recompute commitment → must match published hash
   b. Recompute spin_hash and remap → must match winning number
```

### Verification Endpoint

```
GET /api/verify/roulette/{spin_id}
Response: {
  spin_id,
  commitment,
  server_seed,
  client_seed,
  weight_map,
  raw_index,
  final_pocket,
  winning_number,
  timestamp
}
```

---

## 🗄️ State Management

Roulette state is significantly lighter than card games since there is no persistent game state between spins. However, the betting layout and active spin state are maintained in Redis.

### Redis Key Schema

```
roulette:session:{session_id}:bets         → JSON: current betting layout
roulette:session:{session_id}:spin         → JSON: active spin state
roulette:session:{session_id}:commitment   → string: HMAC commitment for current spin
roulette:session:{session_id}:history      → JSON array: last 200 spin results
roulette:session:{session_id}:hot_cold     → JSON: frequency map for current session
```

### Betting Layout Object

```json
{
  "session_id": "uuid",
  "player_id": "uuid",
  "variant": "EUROPEAN",
  "bets": [
    { "type": "STRAIGHT", "number": 17, "amount": 10.00 },
    { "type": "RED", "amount": 25.00 },
    { "type": "COLUMN", "column": 2, "amount": 15.00 }
  ],
  "total_wagered": 50.00,
  "status": "BETTING_OPEN | SPINNING | RESOLVED"
}
```

### Spin Result Object

```json
{
  "spin_id": "uuid",
  "winning_number": 17,
  "colour": "RED",
  "parity": "ODD",
  "range": "LOW",
  "dozen": "SECOND",
  "column": 2,
  "payout_breakdown": [
    { "bet_type": "STRAIGHT", "number": 17, "win": 350.00 },
    { "bet_type": "RED", "win": 25.00 },
    { "bet_type": "COLUMN", "column": 2, "win": 30.00 }
  ],
  "total_payout": 405.00,
  "net_result": 355.00
}
```

### TTL Policy

| State | TTL |
|---|---|
| Betting layout (open) | 120 seconds (betting window) |
| Active spin | 30 seconds |
| Spin history | 7 days (then archived to PostgreSQL) |

---

## 🖥️ UI — Canvas Wheel Animation

The Roulette wheel is rendered using an HTML5 `<canvas>` element with a WebGL-accelerated animation pipeline.

### Wheel Render Components

```
┌─────────────────────────────────────┐
│   Canvas 1: Static Wheel Base       │  ← Pre-rendered pocket labels, colours
│   Canvas 2: Rotating Wheel Layer    │  ← Animates at variable RPM
│   Canvas 3: Ball Physics Layer      │  ← Ball trajectory and bounce physics
│   DOM Overlay: Result Banner        │  ← Win number, payout summary
└─────────────────────────────────────┘
```

### Spin Animation Sequence

```
Phase 1 — BETTING OPEN (0–20s):
  Wheel spins slowly at constant 30 RPM (decorative)
  Bet grid is interactive

Phase 2 — NO MORE BETS (0.5s):
  "No More Bets" overlay fades in
  Bet grid becomes non-interactive
  Ball enters from rim

Phase 3 — SPIN ACCELERATION (2s):
  Wheel accelerates to 180 RPM
  Ball spins opposite direction at 240 RPM on outer rim
  Ball physics: centrifugal force keeps ball on outer track

Phase 4 — DECELERATION (3s):
  Wheel decelerates toward target pocket (pre-computed)
  Ball speed decreases, falls inward across deflectors
  Ball bounce physics: 4–8 realistic deflector bounces

Phase 5 — POCKET LANDING (1s):
  Ball settles in winning pocket
  Winning number illuminated with spotlight effect
  Wheel slows to stop

Phase 6 — RESULT (persistent):
  Winning number announced in result banner
  Win/Loss animations on betting layout
  Confetti for large wins (>50× total wager)
```

### Ball Physics Model

The ball trajectory is pre-computed from the known winning pocket but visualised with procedural physics noise to appear natural:

```
ball_arc(t) = target_pocket_angle + noise_function(t, deflector_seed)
deflector_seed = spin_hash[0:4]  // First 4 bytes of HMAC output used for visual noise only
```

> The deflector seed only affects the visual path of the ball — the winning pocket is cryptographically determined before animation begins.

### Hot/Cold Numbers Display

A collapsible panel shows frequency data for the current session:

```
🔥 Hot Numbers:  17 (8×), 5 (7×), 32 (6×)
🧊 Cold Numbers: 0 (0×), 13 (1×), 28 (1×)
Last 20 results: [32][17][5][0][22]...[17]
```

### Keyboard Shortcuts

| Key | Action |
|---|---|
| `Space` | Spin (when betting is closed or to rebet) |
| `R` | Rebet (repeat last bet layout) |
| `C` | Clear all bets |
| `U` | Undo last chip placement |
| `1–5` | Select chip denomination |

---

## 🔗 Related Documents

- `GameDesign/Brain_RTP_System.md` — Full Brain architecture
- `GameDesign/ProvablyFair_Specification.md` — HMAC verification standard
- `GameDesign/SessionManagement.md` — Redis state lifecycle
