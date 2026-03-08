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
