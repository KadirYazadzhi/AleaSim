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
