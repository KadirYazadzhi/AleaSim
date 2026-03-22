using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxMultiplierToTournamentEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("4a082bcb-fa52-4fd8-96a2-8544f0276f8f"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("a42f0f79-f04c-4bd1-9edc-00e2202f6a32"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("d72b7e4d-f80d-41e9-91a9-b48b31e329fc"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("e3e970d8-58f6-4d46-8f11-74bc1481004b"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("ee7bb863-4acc-43c1-b3a3-5f6a1d555c5e"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("10c19dc9-6206-4f52-9cd5-bcd09cac1ed0"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("42e1c11e-24c2-4557-a547-ebb1e063d143"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("9c9ecf17-585b-4ce6-8b64-337cbe3ec1c8"));

            migrationBuilder.AddColumn<decimal>(
                name: "MaxMultiplier",
                table: "TournamentEntries",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("134c81fe-96dd-4c36-b3b5-24f4b900398c"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("a4c398d7-b87b-48d1-953f-3cc23ed92926"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("a588e5e0-3493-4190-9195-467ec9772fd5"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("acd56a31-0f96-42e8-9ad8-13c26ed5708d"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("c7faf121-f673-403e-bd0c-1328178316ca"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6354));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6356));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6355));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6352));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6350));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 22, 4, 42, 27, 789, DateTimeKind.Utc).AddTicks(6353));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("bb4359db-e5c6-42e3-803f-c5fba96692df"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" },
                    { new Guid("bdd31440-6afb-469d-8b3a-abdd48a55269"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("bf55bf7c-7a45-45ff-8762-89fb25f1d733"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("134c81fe-96dd-4c36-b3b5-24f4b900398c"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("a4c398d7-b87b-48d1-953f-3cc23ed92926"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("a588e5e0-3493-4190-9195-467ec9772fd5"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("acd56a31-0f96-42e8-9ad8-13c26ed5708d"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("c7faf121-f673-403e-bd0c-1328178316ca"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("bb4359db-e5c6-42e3-803f-c5fba96692df"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("bdd31440-6afb-469d-8b3a-abdd48a55269"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("bf55bf7c-7a45-45ff-8762-89fb25f1d733"));

            migrationBuilder.DropColumn(
                name: "MaxMultiplier",
                table: "TournamentEntries");

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("4a082bcb-fa52-4fd8-96a2-8544f0276f8f"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("a42f0f79-f04c-4bd1-9edc-00e2202f6a32"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("d72b7e4d-f80d-41e9-91a9-b48b31e329fc"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("e3e970d8-58f6-4d46-8f11-74bc1481004b"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("ee7bb863-4acc-43c1-b3a3-5f6a1d555c5e"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8133));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8136));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8135));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8130));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8128));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 21, 1, 40, 9, 726, DateTimeKind.Utc).AddTicks(8132));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("10c19dc9-6206-4f52-9cd5-bcd09cac1ed0"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("42e1c11e-24c2-4557-a547-ebb1e063d143"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" },
                    { new Guid("9c9ecf17-585b-4ce6-8b64-337cbe3ec1c8"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" }
                });
        }
    }
}
