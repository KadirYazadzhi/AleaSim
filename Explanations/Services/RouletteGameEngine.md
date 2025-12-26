# RouletteGameEngine - European Roulette Implementation

`RouletteGameEngine.cs` simulates a standard single-zero European Roulette wheel.

## ⚙️ Configuration
- **Wheel**: Integers 0-36.
- **Red Numbers**: Hardcoded array of standard red numbers (1, 3, 5, etc.).
- **House Edge**: The presence of `0` (Green) provides the house edge, as it is neither Red/Black nor Even/Odd.

## 🎲 Betting Logic
The engine supports complex "Multi-Bets". A user can place 10 different chips in one request.
- **Input Format**: JSON `[{"Type":"Color","Value":"Red","Amount":10}, ...]`.
- **Validation**: Ensures the sum of all chips equals the `totalBetAmount` deducted from the wallet.

## 🏆 Win Calculation (`CalculateBetWin`)
Iterates through every chip placed:
1.  **"Number"**: Straight up bet. Pays 35:1 (Logic says `* 36` which includes the returned bet).
2.  **"Color"**: Checks if the winning number is in `_redNumbers`. Pays 1:1 (`* 2`).
    - **Rule**: If result is `0`, color bets lose.
3.  **"EvenOdd"**: Checks `num % 2`. Pays 1:1.
    - **Rule**: If result is `0`, even/odd bets lose.

## ⚖️ RTP Enforcement
Roulette is high variance. A user can win 35x their bet.
- **Check**: If the total win is huge, `RtpEngine.IsOutcomeAllowed` is called.
- **Intervention**: If denied, the engine forces the win to `0`. *Note: In a real casino, this is illegal. The engine would instead re-roll the result to a losing number rather than simply confiscating the win.*
