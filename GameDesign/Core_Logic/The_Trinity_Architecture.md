# 🏗️ The Trinity Architecture: Brain, CMS, & Vault

## Overview
This document outlines the architectural paradigm shift for **AleaSim**. Moving away from a standard RNG-based "Slot Machine" model, we are implementing a **Behavioral Retention Engine**.

The system is divided into three distinct logical components, known as "The Trinity". Each has strict responsibilities and boundaries.

---

## 1. THE BRAIN (The CEO / Decision Maker)
*   **Status:** Omniscient & Omnipotent.
*   **Responsibility:** Determines **WHAT** happens.
*   **Knowledge:** Knows everything about the user, their history, psychology, risk of churning, and the casino's current financial health.
*   **Logic:** It does not roll dice. It makes calculated business decisions to maximize Player LTV (Lifetime Value) and retention.
*   **Output:** A `DecisionDirective` (e.g., "User must win 20x bet to prevent churn" or "User is too lucky, force a near-miss").

## 2. THE CMS (The Artist / The Library)
*   **Status:** Dumb / Servile.
*   **Responsibility:** Determines **HOW** it looks.
*   **Knowledge:** Knows game rules, paytables, symbol assets, and reel strips. Does NOT know about user balances or history.
*   **Logic:** **Reverse Engineering**. It receives a `DecisionDirective` from The Brain (e.g., "Target Win: $50") and searches its mathematical permutations to construct a visual representation that matches that result.
*   **Output:** A `GameRoundResult` containing symbol positions and visual metadata.

## 3. THE VAULT (The RTP / The CFO)
*   **Status:** Strict / Validator.
*   **Responsibility:** Determines if we can **AFFORD** it.
*   **Knowledge:** Knows the Global Pool, User's Personal RTP (pRTP), and Transaction History.
*   **Logic:** Solvency Check. It does not care about game fun. It cares about math. It holds the "Hard Limits".
*   **Output:** `TransactionApproval` or `Rejection` (forcing The Brain to rethink).

---

## The Interaction Flow

1.  **User Action:** User clicks "SPIN".
2.  **Data Gathering:** System pulls User Profile (LTV, Session Luck, pRTP).
3.  **Brain Analysis:**
    *   *Brain:* "Player is losing. Risk of quit: High. pRTP is low (60%)."
    *   *Brain:* "Decision: Give a 'Sugar Hit' (Small Win approx 5x-10x bet)."
4.  **CMS Execution:**
    *   *CMS:* "I need to visualize a 10x win on the Slot Engine."
    *   *CMS:* *Scans Paytable...* "Found combination: 3 Lemons."
    *   *CMS:* "Setting Reel Stops to display 3 Lemons."
5.  **Vault Validation:**
    *   *Vault:* "Checking Pool. We have funds. Transaction Approved."
6.  **Response:** User sees the reels spin and land on 3 Lemons. They feel lucky. The Brain succeeded.
