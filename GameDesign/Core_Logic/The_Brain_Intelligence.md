# 🧠 The Brain: Intelligence & Decision Logic

## Philosophy
The Brain is the core differentiator of AleaSim. It treats gambling not as a game of chance, but as a **Managed Experience**. Its goal is not "Fairness" in the random sense, but **Engagement Optimization**.

## Data Inputs (The Context)
To make a decision, The Brain consumes the following real-time metrics:

### 1. Session Metrics (Short Term)
*   **Session Duration:** How long has the user been active?
*   **Current Mood:** Is the user changing games rapidly? (Sign of boredom/frustration).
*   **Session Balance:** Is the user up or down *in this specific session*?
*   **Loss Streak:** How many consecutive losses? (Crucial for "Pity Wins").

### 2. Profile Metrics (Long Term)
*   **LTV (Lifetime Value):** Total Net Deposit. High LTV players get better perks.
*   **pRTP (Personal Return to Player):** The actual percentage returned to this specific user over their history.
    *   *Goal:* Keep pRTP close to theoretical (e.g., 95%).
    *   *If pRTP < 80%:*: User is "Unlucky". Brain forces wins to correct.
    *   *If pRTP > 120%:*: User is "Winning too much". Brain forces "Cool Down".
*   **Volatility Preference:** Does this user prefer small consistent wins (Low Vol) or rare massive wins (High Vol)?

---

## The Decision Algorithm

The Brain evaluates rules in a prioritized hierarchy:

### Priority 1: Retention (The "Hook")
*   **Condition:** User has lost > 30% of wallet in < 10 mins OR Loss Streak > 8.
*   **Action:** **Force Win.**
*   **Magnitude:** 15x - 20x Bet. Enough to recover ~50% of session losses.
*   **Goal:** The "Relief" dopamine hit.

### Priority 2: Correction (The "Rubber Band")
*   **Condition:** pRTP is significantly deviant from Target RTP (e.g., User pRTP is 40%).
*   **Action:** **Boost Luck.**
*   **Logic:** Instead of standard outcomes, generate outcomes from a "Lucky" distribution table until pRTP normalizes.

### Priority 3: Cooling (The "Brake")
*   **Condition:** User just won a Massive Jackpot or pRTP > 200%.
*   **Action:** **Force Near Miss.**
*   **Logic:** Do not give more money immediately. Generate excitement without payout (e.g., 2 Bonus Symbols + 1 blank). This maintains adrenaline without breaking the bank.

### Priority 4: The Whale Protocol (High Rollers)
*   **Condition:** Bet Amount > $100 (Configurable Threshold).
*   **Action:** **Custom Volatility.**
*   **Logic:** Remove "Small Wins" (1x-2x). Focus only on "Losses" or "Big Wins". High rollers find small wins insulting.

---

## Output to CMS
The Brain sends a **Directive Object**:
```json
{
  "DirectiveType": "ForceWin",
  "TargetMultiplier": 15.0,
  "AllowedDeviation": 0.1, // +/- 10%
  "VisualTheme": "Standard", // or "BonusRound"
  "Reason": "RetentionHook" // For Analytics
}
```
