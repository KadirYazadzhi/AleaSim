# Game Design: Fruit Blast (Nuclear Fruits)

## 1. Overview
**Fruit Blast** is a modern, high-volatility "Cascading" or "Avalanche" style slot game. Instead of spinning reels, symbols fall from the top of the grid. It features explosive mechanics, chain reactions, and multipliers.

## 2. Core Mechanics
- **Grid Layout**: 6x5 (30 symbols total).
- **Win Mechanic**: Cluster Pays (8+ matching symbols anywhere on the grid) or 20 Fixed Paylines (TBD).
- **Avalanche System**: When a win occurs, the winning symbols explode and disappear. New symbols fall from above to fill the gaps. This continues as long as new wins are formed.

## 3. Special Symbols: The Bombs
Bombs appear randomly during any fall and trigger after all wins for that specific "drop" are calculated.

- **Small Bomb (TNT)**: 
    - **Effect**: Explodes in a 3x3 radius.
    - **Outcome**: Destroys symbols and triggers a fresh fall.
- **Nuclear Bomb (The Core)**:
    - **Requirement**: 2+ bombs on screen.
    - **Effect**: Connects all bombs with an energy beam, destroying entire rows and columns between them.
- **Supernova Bomb**:
    - **Requirement**: 4+ bombs on screen.
    - **Effect**: Destroys the entire grid.
    - **Bonus**: Grants a persistent multiplier (x2 to x100) for the remainder of the round's avalanche sequence.

## 4. Progressive Multiplier (The Juice Meter)
- Every explosion fills the **Juice Meter**.
- **Level 1 (5 Explosions)**: Low-tier symbols (Lemon, Cherry) pay 2x.
- **Level 2 (10 Explosions)**: Mid-tier symbols (Orange, Plum) pay 3x.
- **Level 3 (20 Explosions)**: All symbols become "Golden" and pay 10x base value.

## 5. Mathematical Profile (AI "The Brain" Integration)
- **RTP**: 96.8% (Target).
- **Volatility**: Very High (Level 5/5).
- **Hit Frequency**: Moderate, but with high potential for long cascading sequences.
- **Brain Role**: The Brain decides when to drop a "Saver Bomb" (to keep a player engaged during a loss streak) or a "Nuclear Bomb" (when the user is in a high-reward flow state).

## 6. Visual Style
- **Theme**: Neon-lit laboratory meets classic fruit market.
- **Effects**: Particle explosions, screen shakes, and slow-motion for Big Wins.
- **Background Music**: High-tempo electronic beats that speed up as the Juice Meter fills.

## 7. Development Phases
1. **Phase 1**: Backend logic for Cascading falls and recursive win calculations.
2. **Phase 2**: PixiJS implementation of gravity and explosion particles.
3. **Phase 3**: Integration with the Quest and Jackpot systems.

---

## 8. Symbol Paytable

Wins are awarded for **clusters** of matching symbols (8+ connected symbols of the same type anywhere on the grid). Values are **multipliers of the Total Bet**.

| Symbol Tier | Symbol | 8-cluster | 10-cluster | 12-cluster | 15-cluster | 20+-cluster |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Premium** | 🍇 Grape | 2x | 4x | 8x | 20x | 50x |
| **Premium** | 🍓 Strawberry | 1.5x | 3x | 6x | 15x | 40x |
| **Mid** | 🍊 Orange | 1x | 2x | 4x | 10x | 25x |
| **Mid** | 🍑 Peach | 0.8x | 1.5x | 3x | 8x | 20x |
| **Low** | 🍋 Lemon | 0.5x | 1x | 2x | 5x | 12x |
| **Low** | 🍒 Cherry | 0.4x | 0.8x | 1.5x | 4x | 10x |
| **Wild** | ⚡ Electric Fruit | Substitutes for all symbols — no direct cluster pay |
| **Scatter** | 💣 Bomb | See Section 9 — triggers explosion mechanic |

> All values are **before** Juice Meter multipliers are applied. At Juice Meter Level 3, all symbol pays are ×10 base value.

---

## 9. Bomb Probability Table

Bomb drops are evaluated once per cascade fall, after standard cluster wins are resolved.

| Bomb Type | Normal Conditions | Brain-Influenced (Loss Streak ≥ 5) | Brain-Influenced (Flow State) |
| :--- | :---: | :---: | :---: |
| **TNT (Small)** | 8.0% per drop | 18.0% per drop | 5.0% per drop |
| **Nuclear (The Core)** | 1.2% per drop | 4.5% per drop | 0.8% per drop |
| **Supernova** | 0.15% per drop | 0.80% per drop | 0.10% per drop |

> **Notes:**
> - Nuclear Bomb requires 2+ bombs already present on the grid to trigger its chain effect.
> - Supernova requires 4+ bombs on the grid. If fewer are present, it downgrades to Nuclear.
> - The Brain's "Saver Bomb" directive forces a TNT drop on the next cascade if no win has occurred for 3+ consecutive drops in the current round.
> - Probabilities are independent per drop event; multiple bombs can appear in the same fall.

---

## 10. Juice Meter Level Details

| Level | Explosions Required (Cumulative) | Effect | Multiplier Applied |
| :---: | :---: | :--- | :---: |
| **0** (Inactive) | 0 | No effect; standard pays | 1x |
| **1** | 5 | Low-tier symbols (Lemon, Cherry) pay at boosted rate | 2x on low-tier only |
| **2** | 10 | Mid-tier symbols (Orange, Peach) also boosted | 3x on mid + low tier |
| **3** (Golden) | 20 | All symbols become "Golden"; entire grid pays at max rate | 10x on all symbols |

> The Juice Meter resets to 0 at the end of each complete round (after all cascades exhaust). It does **not** carry over between spins unless the Brain is in Flow State mode (then Level 1 is preserved as the starting point for the next round).

---

## 11. State Persistence in Redis

All mid-round state is written to Redis to support stateless HTTP requests between cascade steps.

| Redis Key | Value Type | Contents | TTL |
| :--- | :--- | :--- | :--- |
| `game:fruitblast:{sessionId}:grid` | JSON string | Current 6×5 symbol grid (30 cells with symbol IDs) | 30 min |
| `game:fruitblast:{sessionId}:juice` | Integer | Current Juice Meter level (0–3) | 30 min |
| `game:fruitblast:{sessionId}:cascade` | Integer | Cascade count within current round | 30 min |
| `game:fruitblast:{sessionId}:betAmount` | Decimal string | Locked bet amount for this round | 30 min |
| `game:fruitblast:{sessionId}:multiplier` | Decimal string | Supernova persistent multiplier (if active) | 30 min |
| `game:fruitblast:{sessionId}:bombs` | JSON array | Positions and types of locked bombs this fall | 30 min |

On round completion (all cascades exhausted, no active bombs), all keys for that session are deleted. On session expiry (TTL), incomplete rounds are abandoned and the bet is fully refunded via the Vault rollback mechanism.

---

## 12. Brain Integration Details

### Saver Bomb Trigger Conditions
The Brain injects a guaranteed TNT bomb on the next cascade drop when **any** of the following conditions are met:

| Condition | Threshold | Brain Action |
| :--- | :--- | :--- |
| Consecutive loss rounds (no win at all) | ≥ 5 rounds | Force TNT drop on next fall |
| Global pool balance below floor | `GlobalPool < MinFloor * 1.1` | Suppress all bombs (near-miss mode) |
| Player loss streak in current session | ≥ 8 rounds | Force TNT + elevate Nuclear probability |
| AvgSpinInterval increase vs session baseline | > 40% slower | Force TNT to re-engage player |
| pRTP delta too negative | Player RTP < `TargetRTP - 15%` | Force Nuclear drop |

### Flow State Bomb Suppression
When the Brain detects Flow State (fast play < 2.5s per spin, positive pRTP delta), bomb probabilities are **reduced** (see Section 9) to preserve volatility and allow the Vault to reclaim balance from a lucky session.

### Directive Injection Mechanism
The Brain writes a `BombDirective` object to Redis key `brain:directive:{userId}` before the spin is processed. The Fruit Blast game engine reads this key at cascade resolution time and applies the override. The directive is consumed (deleted) after one use and never persists past the current round.

---

## 13. Development Status

| Phase | Description | Status |
| :--- | :--- | :--- |
| Phase 1 | Backend cascade logic and recursive win calculation | ✅ Complete |
| Phase 2 | PixiJS gravity physics and explosion particle effects | 🔄 In Progress |
| Phase 3 | Quest and Jackpot system integration | ⏳ Planned |
| Phase 4 | Brain integration (Saver Bomb + Flow State tuning) | ⏳ Planned |
| Phase 5 | Math certification and RTP validation | ⏳ Planned |
