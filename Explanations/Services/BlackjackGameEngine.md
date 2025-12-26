# BlackjackGameEngine - Card Game Implementation

`BlackjackGameEngine.cs` simulates a simplified version of Blackjack (21). It is a **Stateful** game, meaning the result isn't instant; it spans multiple requests (Deal -> Hit -> Hit -> Stand).

## 🧠 State Management (`BlackjackState`)
Since HTTP is stateless, the engine stores the current "Table" in memory:
- **`PlayerHand`**: List of cards currently held by user.
- **`DealerHand`**: List of cards held by dealer.
- **`IsRoundOver`**: Flag to prevent "Hitting" after the game ends.
- **`Sequence`**: Counts how many cards have been drawn to ensure the RNG produces a fresh card next time.

## 🃏 Game Logic Breakdown

### 1. `ResolveRound` (The Deal)
- Triggered when the user places a bet.
- Deals 4 cards in alternating order: Player, Dealer, Player, Dealer.
- **Instant Win Check**: If Player has 21 (Ace + 10/Face), it ends the round immediately (Blackjack).

### 2. `ProcessAction` (Player Turn)
- **"Hit"**: Draws one card. Checks for Bust (>21).
- **"Stand"**: Ends player turn, triggers Dealer Logic.

### 3. Dealer Logic (`FinishRound`)
- The dealer **must** draw cards until their total is 17 or higher.
- This is a standard casino rule ("Dealer stands on 17").

### 4. Win Evaluation
- **Bust**: Player > 21 (Loss).
- **Dealer Bust**: Dealer > 21 (Win).
- **Comparison**: Player Total vs Dealer Total. Higher wins.
- **Push**: Totals are equal (Bet returned).

## 🔢 Card Encoding & Math
- **Card Drawing**: `RngService.GetNextInt(..., 0, 52)`.
- **Mapping**:
    - Index `0-12` = Hearts (A, 2...K).
    - Index `13-25` = Diamonds, etc.
- **Value Calculation**:
    - Face cards (J, Q, K) = 10.
    - Ace = 11, unless total > 21, then Ace = 1. (Dynamic calculation implemented in `CalculateHandValue`).
