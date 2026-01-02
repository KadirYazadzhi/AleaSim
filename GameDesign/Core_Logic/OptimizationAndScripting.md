# Technical Architecture: Optimization & Scripting

## 1. Data Strategy: Hot vs. Cold
To handle millions of spins without DB crashes, we use a tiered data approach.

### Tier 1: Hot State (In-Memory / Cache)
*   **Storage:** Redis or In-Memory Dictionary (Concurrent).
*   **Content:** The Active Session State.
    *   `SessionId`
    *   `CurrentScript` (If running a scripted bonus)
    *   `ActiveGameState` (Grid, StickySymbols, RespinsLeft, LockedBetAmount)
*   **Speed:** < 1ms access.
*   **Persistence:** Volatile. Saved to DB only on "Session End" or "Critical Checkpoint" (Bonus Entry).

### Tier 2: Cold Storage (Database)
*   **Storage:** SQL / NoSQL.
*   **Content:**
    *   Completed Rounds (Audit Logs).
    *   Financial Transactions (Balance Updates).
    *   User Profiles.
*   **Write Strategy:** Batch writing (Write-Behind) or Critical-Only writes. We do NOT write every spin state to SQL unless necessary for crash recovery.

## 2. The Script Engine (Reverse Math)
The core of the "Illusion of Control". Instead of generating random numbers, we generate **Outcomes**.

### Workflow
1.  **Objective:** Brain sets a goal (e.g., "Win 50.00 BGN").
2.  **Blueprint Selection:** Engine picks a template matching the goal (e.g., "Bell Bonus with Mini").
3.  **Filling:** Algorithm distributes the win amount into specific symbols.
    *   *Problem:* 50.00 needs to be split into 5 bells.
    *   *Solution:* 20 (Mini) + 10 + 10 + 5 + 5.
4.  **Pacing (The Drama):** The script distributes these events over time to create an emotional curve.
    *   *Start:* Hope (2 bells).
    *   *Middle:* Fear (2 dead spins).
    *   *Climax:* Relief (Mini Jackpot lands).
5.  **Execution:** The produced `ScriptQueue` is stored in Hot State.
    *   Next Spin Request -> Pop item from Queue -> Return to Frontend.
    *   Zero CPU cost during the actual spin.

## 3. Benefits
*   **Performance:** Complex math is done ONCE (at trigger). Serving spins is just array indexing.
*   **Control:** Exact control over RTP and User Experience.
*   **Audit:** If a user complains, we can show the generated script plan.
