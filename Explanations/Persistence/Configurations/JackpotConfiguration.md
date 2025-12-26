# JackpotConfiguration - Financial Pool Schema

Configures the `Jackpot` entity, requiring high precision and concurrency control.

## 🛠️ Schema Rules

### Precision
- **`CurrentValue`**: `(18, 2)`. Standard currency.
- **`ContributionRate`**: `(18, 5)`.
    - **Why?** A rate like `0.5%` is `0.005`. However, sometimes casinos use very small fractions like `0.125%`. Using 5 decimal places (`0.00125`) ensures these small tax rates are stored accurately without rounding down to zero.

### 🔒 Concurrency Control
```csharp
builder.Property(j => j.LastUpdated).IsConcurrencyToken();
```
- **Concept**: Optimistic Concurrency.
- **Scenario**: Two players hit the jackpot at the exact same millisecond.
- **Mechanism**:
    1.  EF Core reads the record, remembering `LastUpdated` was `12:00:00`.
    2.  When saving, it runs SQL: `UPDATE ... WHERE Id=X AND LastUpdated='12:00:00'`.
    3.  If another thread updated it to `12:00:01` in the meantime, the `WHERE` clause fails (0 rows affected).
    4.  EF Core throws `DbUpdateConcurrencyException`.
- **Result**: Prevents the "Lost Update" problem where the jackpot could be paid out twice or resets incorrectly.
