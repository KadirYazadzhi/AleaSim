# 🃏 Blackjack Game Design Document

> **Platform:** AleaSim Casino  
> **Version:** 1.0  
> **Status:** Draft

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Standard Rules](#standard-rules)
3. [Hand Types](#hand-types)
4. [Dealer Rules](#dealer-rules)
5. [Player Actions](#player-actions)
6. [Paytable](#paytable)
7. [Side Bets](#side-bets)
8. [House Edge](#house-edge)
9. [Brain Integration](#brain-integration)
10. [Provably Fair Shuffle](#provably-fair-shuffle)
11. [State Management](#state-management)
12. [UI Flow](#ui-flow)

---

## 🎯 Overview

Blackjack is AleaSim's flagship table game. The objective is to beat the dealer by drawing cards with a total value closer to 21 than the dealer, without exceeding 21 (busting). AleaSim implements standard Las Vegas–style Blackjack using a configurable number of decks (default: 6-deck shoe), with full provably-fair verification of each shuffle.

---

## 📜 Standard Rules

| Rule | Value |
|---|---|
| Decks in shoe | 6 (configurable: 1, 2, 4, 6, 8) |
| Blackjack pays | 3:2 |
| Dealer hits on | Soft 17 |
| Double Down | Any two cards |
| Split | Up to 3 times (4 hands max) |
| Split Aces | One card each, no re-split |
| Surrender | Late surrender allowed |
| Insurance | Offered when dealer shows Ace |
| Push | Bet returned |

### Card Values

| Card | Value |
|---|---|
| 2 – 10 | Face value |
| Jack, Queen, King | 10 |
| Ace | 1 or 11 (see Hand Types) |

---

## ✋ Hand Types

### Hard Hands

A **hard hand** contains no Ace, or contains an Ace that must count as 1 to avoid busting.

| Example | Total |
|---|---|
| 9 + 7 | Hard 16 |
| K + 6 | Hard 16 |
| A + 9 + 8 | Hard 18 (Ace forced to 1) |

### Soft Hands

A **soft hand** contains an Ace counted as 11 without busting. Soft hands provide flexibility — the Ace can revert to 1 if the next card would cause a bust.

| Example | Soft Total | If hit with 9 |
|---|---|---|
| A + 6 | Soft 17 | 16 (Ace becomes 1) |
| A + 7 | Soft 18 | 17 (Ace becomes 1) |
| A + 5 | Soft 16 | Soft 25 → Hard 15 |

> 💡 **Key Rule:** Dealer must hit on Soft 17 (A+6), giving the house a marginal edge over a stand-on-soft-17 rule.

### Blackjack (Natural)

An Ace + any 10-value card on the initial deal = Blackjack. Blackjack beats a dealer 21 made from three or more cards.

---

## 🎰 Dealer Rules

The dealer follows a fixed, deterministic strategy — no discretion:

1. Dealer always **hits** when total ≤ 16
2. Dealer always **hits** on **Soft 17** (A + 6)
3. Dealer always **stands** on Hard 17 through 21
4. Dealer **stands** on Soft 18 through Soft 21
5. Dealer's hole card is revealed only after the player completes all actions

### Dealer Reveal Sequence

```
Player finishes → Dealer flips hole card → Dealer draws until rule satisfied → Outcome resolved
```

---

## 🕹️ Player Actions

### Hit
Draw one additional card. Available until bust (>21) or stand.

### Stand
Decline further cards. End the player's turn for that hand.

### Double Down
Double the initial wager and receive exactly one more card. Available on any two-card hand. Recommended on Hard 9, 10, 11 by basic strategy.

### Split
When holding two cards of equal value, split into two separate hands, each receiving a new second card. A second bet equal to the original is placed on the new hand.

- **Split Aces:** Receive exactly one card per Ace. No further actions allowed on split Ace hands.
- **Maximum splits:** 3 (resulting in 4 simultaneous hands)
- **Double after split:** Allowed on non-Ace splits

### Surrender (Late)
Forfeit the hand after the dealer checks for Blackjack. Player recovers 50% of the wager. Not available on split hands.

### Insurance
Offered when the dealer's up-card is an Ace. Player may wager up to half of the original bet. Pays 2:1 if dealer has Blackjack. Insurance is a separate side wager resolved immediately after dealer checks the hole card.

> ⚠️ **Basic strategy note:** Insurance has a high house edge (~7.5%) against a random shoe and is generally not recommended. In a Brain-managed game, Insurance outcomes follow the same shoe shuffle commitment as the main hand — the Brain cannot selectively influence whether the hole card is a ten-value card without affecting the entire shoe composition. Therefore, the statistical house edge on Insurance remains consistent with the declared shoe composition, and card-counting strategies retain their theoretical validity.

---

## 💰 Paytable

| Outcome | Payout |
|---|---|
| Player Blackjack (3:2) | +150% of bet |
| Player wins (non-Blackjack) | +100% of bet |
| Push (tie) | Bet returned (0%) |
| Player busts | –100% of bet |
| Dealer busts, player stands | +100% of bet |
| Insurance wins | +200% of insurance bet |
| Insurance loses | –100% of insurance bet |
| Surrender | –50% of bet |

---

## 🎲 Side Bets

Side bets are optional and resolved independently from the main hand using the same provably-fair shoe.

### Perfect Pairs

Placed before the deal. Wins if the player's first two cards form a pair.

| Pair Type | Description | Payout |
|---|---|---|
| Mixed Pair | Same rank, different colour | 5:1 |
| Colored Pair | Same rank, same colour, different suit | 10:1 |
| Perfect Pair | Same rank, same suit (identical) | 25:1 |

### 21+3 (Three Card Poker Bonus)

Uses the player's two cards and the dealer's up-card to form a three-card poker hand.

| Hand | Description | Payout |
|---|---|---|
| Flush | Three cards of same suit | 5:1 |
| Straight | Three consecutive ranks | 10:1 |
| Three of a Kind | Three cards of same rank | 30:1 |
| Straight Flush | Consecutive ranks, same suit | 40:1 |
| Suited Three of a Kind | Same rank AND same suit | 100:1 |

---

## 📊 House Edge

The house edge in Blackjack is the lowest of any casino game when optimal basic strategy is applied.

| Configuration | House Edge |
|---|---|
| 6-deck, dealer hits soft 17, late surrender | ~0.50% |
| 6-deck, dealer stands soft 17, no surrender | ~0.40% |
| Single-deck, dealer hits soft 17 | ~0.17% |
| Perfect Pairs side bet | ~5.7% |
| 21+3 side bet | ~3.2% |

### Edge Components

```
Base house edge contributors:
  Dealer hits soft 17:        +0.22%
  6 decks vs 1 deck:          +0.60%
  Late surrender allowed:     −0.08%
  Double after split:         −0.14%
  Re-splits (up to 3):        −0.05%
  ─────────────────────────────────
  Net house edge (approx):    ~0.50%
```

---

## 🧠 Brain Integration

The Brain is AleaSim's RTP management subsystem. In Blackjack, the Brain **cannot force a specific card** to be dealt — doing so would break provably-fair verification. Instead, it operates at the **shoe composition** level.

### Mechanism: Probability-Weighted Shoe Shuffle

The Brain influences Blackjack outcomes through shoe composition biasing at shuffle time:

1. **Pre-shuffle hook:** Before generating the shuffle seed, the Brain evaluates the current RTP state for the session and platform.
2. **Shoe variant selection:** The Brain selects from a pre-validated set of shoe compositions (e.g., slightly high-card-rich or low-card-rich shoes) that are within regulatory RTP bounds.
3. **Seed binding:** The selected shoe composition variant is encoded into the HMAC-SHA256 commitment. The player receives the commitment hash before any cards are dealt.
4. **Transparency:** Because the full shoe is committed before play begins, the Brain cannot retroactively change outcomes — it only influences the statistical distribution of shoes selected.

### RTP Management Modes

| Mode | Brain Behaviour |
|---|---|
| `NORMAL` | Uniform random shoe selection |
| `COOLING` | Slight bias toward house-favourable shoe compositions |
| `HEATING` | Slight bias toward player-favourable shoes (retention play) |

> 🔒 **Constraint:** All shoe compositions remain within a ±0.3% variance of the declared house edge. The Brain cannot select shoes outside this regulatory envelope.

---

## 🔐 Provably Fair Shuffle

AleaSim uses HMAC-SHA256 to allow players to verify that the shoe was not altered after commitment.

### Shuffle Verification Flow

```
1. Server generates:
   - server_seed  (secret, random 256-bit value)
   - shoe_data    (ordered array of 312 cards for 6 decks)

2. Server computes and publishes commitment:
   commitment = HMAC-SHA256(server_seed, shoe_data)

3. Player is shown `commitment` BEFORE the first card is dealt.

4. Player may optionally provide a client_seed (default: random UUID).

5. Final shoe order is derived:
   final_shoe = Fisher-Yates(shoe_data, HMAC-SHA256(server_seed, client_seed))

6. After shoe is exhausted (or player requests), server reveals:
   - server_seed
   - shoe_data
   - client_seed

7. Player can independently verify:
   - Recompute commitment and check it matches step 2.
   - Recompute Fisher-Yates shuffle and confirm card order matches game history.
```

### Verification Endpoint

```
GET /api/verify/blackjack/{session_id}
Response: { commitment, server_seed, client_seed, shoe_data, hand_history }
```

---

## 🗄️ State Management

Active Blackjack game state is stored in **Redis** with a configurable TTL.

### Redis Key Schema

```
blackjack:session:{session_id}:state       → JSON: full game state
blackjack:session:{session_id}:shoe        → JSON: remaining shoe (ordered card array)
blackjack:session:{session_id}:commitment  → string: HMAC-SHA256 commitment hash
blackjack:session:{session_id}:server_seed → string: (encrypted at rest)
```

### Game State Object

```json
{
  "session_id": "uuid",
  "player_id": "uuid",
  "status": "AWAITING_ACTION | DEALER_TURN | RESOLVED",
  "hands": [
    {
      "hand_index": 0,
      "cards": [{"rank": "A", "suit": "SPADES"}, {"rank": "6", "suit": "HEARTS"}],
      "total_soft": 17,
      "total_hard": 7,
      "is_soft": true,
      "bet": 10.00,
      "status": "ACTIVE | STOOD | BUSTED | BLACKJACK | SURRENDERED"
    }
  ],
  "dealer_hand": {
    "cards": [{"rank": "K", "suit": "CLUBS"}, {"rank": "HIDDEN"}],
    "visible_total": 10
  },
  "side_bets": {
    "perfect_pairs": 5.00,
    "twenty_one_plus_three": 0.00
  },
  "shoe_index": 42,
  "created_at": "ISO8601",
  "updated_at": "ISO8601",
  "ttl_seconds": 900
}
```

### State TTL

| Scenario | TTL |
|---|---|
| Active hand in progress | 900 seconds (15 min) |
| Hand resolved, awaiting next bet | 300 seconds (5 min) |
| Session expired | State archived to PostgreSQL, Redis key deleted |

---

## 🖥️ UI Flow

### Bet Phase
```
[Chip Tray] → Player selects chip denominations → Clicks on bet circle
             → "Deal" button activates → Player confirms bet
```

### Deal Phase
```
Server commits shoe → Cards animate onto table (player L→R, dealer L→R)
                    → Dealer's second card face-down
                    → Side bet results evaluated immediately
                    → Insurance prompt appears if dealer shows Ace
```

### Action Phase
```
Action buttons displayed: [Hit] [Stand] [Double] [Split*] [Surrender*]
(* = conditionally shown based on hand state)

→ Player selects action
→ Card animation (if Hit/Double/Split)
→ If bust: hand greys out, result badge shown
→ If Blackjack on deal: golden badge, 3:2 payout shown immediately
```

### Dealer Phase
```
Dealer hole card flips (animation)
→ Dealer draws cards one-by-one with 600ms delay
→ Each card animates with sound effect
→ Dealer busts / stands → outcome evaluated
```

### Resolution Phase
```
Win:  Green chip animation, balance credited, win amount displayed
Lose: Red fade, bet deducted
Push: Chips returned with neutral animation

→ "Next Hand" button resets table
→ Previous hand summary persists briefly in history strip
```

### Keyboard Shortcuts

| Key | Action |
|---|---|
| `H` | Hit |
| `S` | Stand |
| `D` | Double Down |
| `P` | Split |
| `U` | Surrender |
| `Space` | Deal / Next Hand |

---

## 🔗 Related Documents

- `GameDesign/Brain_RTP_System.md` — Full Brain architecture
- `GameDesign/ProvablyFair_Specification.md` — HMAC verification standard
- `GameDesign/SessionManagement.md` — Redis state lifecycle
