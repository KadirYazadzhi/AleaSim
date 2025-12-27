# BlackjackGameEngine Implementation Explanation

`BlackjackGameEngine.cs` simulates a standard Blackjack game, handling complex state management across multiple user requests.

## 🧠 State Persistence
Unlike Slots (instant result), Blackjack requires the server to "remember" the cards between the initial Deal and the player's Hit/Stand decision.
- **Storage**: The state (Player Hand, Dealer Hand) is serialized to JSON and stored in the `GameRound.RandomResult` (or `InputData`) column in the database.
- **Retrieval**: On `Hit`, it loads the last round, deserializes the JSON back into a `BlackjackState` object, modifies it, and saves it back.

## 🃏 Logic Flow

### `ResolveRound` (The Deal)
1.  Draws 4 cards (Player, Dealer, Player, Dealer).
2.  Checks for instant Blackjack.
3.  Saves the initial state.
4.  Notifies the frontend via `RealTimeService`.

### `ProcessAction` (Hit/Stand)
1.  Loads the state from DB.
2.  **Hit**: Draws a card. If total > 21, triggers `FinishRound` (Bust).
3.  **Stand**: Triggers `FinishRound` (Dealer's Turn).

### `FinishRound` (Dealer Logic)
1.  Dealer hits until >= 17.
2.  Compares totals.
3.  Calculates Win:
    - Blackjack: 2.5x
    - Win: 2x
    - Push: 1x
4.  **RTP Check**: Calls `ProcessWin`. If denied, the win is zeroed (harsh but safe simulation).