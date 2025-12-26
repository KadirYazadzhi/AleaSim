# RTPStatisticsConfiguration - Analytics Schema

Configures the `RTPStatistics` entity for performance monitoring.

## 🛠️ Schema Rules

### Indices
- **`HasIndex(s => s.GameId)`**: Fast lookup for "How is Slot Machine X performing?".
- **`HasIndex(s => s.UserId)`**: Fast lookup for "Is User Y winning too much?".

### Precision
- **`TotalWagered` & `TotalPaid`**: `(18, 2)`.
- **Aggregates**: Since these fields accumulate over time, they can grow very large (millions of dollars). The `(18, 2)` definition supports values up to 10 quadrillion, which is sufficient even for high-volume casinos.
