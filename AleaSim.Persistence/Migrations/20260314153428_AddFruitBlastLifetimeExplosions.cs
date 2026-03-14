using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFruitBlastLifetimeExplosions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("22aa7380-e596-43bf-9e58-e1db17cc52bb"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("37b67d61-c310-45eb-b133-7e43e1c847d5"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("593880d2-d2f0-485f-84e3-198f6dbed3b5"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("e54c597b-1189-444c-8ee6-5187fad2e8d7"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("f1de42a9-d3c1-4def-a93e-576e3a961b3d"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("23e39603-f644-4de3-b3bc-7a109c40862c"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("5ce1bf67-8bf1-4898-808b-5dd1428ad465"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("a56f526b-d91b-41dc-a483-d68ed8887029"));

            migrationBuilder.AddColumn<int>(
                name: "FruitBlastLifetimeExplosions",
                table: "PlayerProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("185a63b8-4c5b-41e3-979b-491cf5046d4c"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("1b815e3f-a1a0-42a3-9256-878257eb77cb"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("c14dd7b3-f2bb-4edf-9035-20347b5fabbc"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("d7fd0f25-dd73-4a3c-ae24-2846e32275d2"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("f685d765-233e-4a0f-ab96-7285eb88d069"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m }
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "ConfigurationJson", "IsActive", "MaxBet", "MinBet", "Name", "PoolBalance", "Provider", "TargetRTP", "Type" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), null, true, 1000m, 0.1m, "Fruit Blast", 0m, "AleaSim Originals", 0.960m, "fruitblast" });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9861));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9864));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9863));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9857));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9854));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 14, 15, 34, 27, 815, DateTimeKind.Utc).AddTicks(9859));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("0f14c298-9718-417e-b80d-13f880ebc713"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" },
                    { new Guid("c4bea8a5-ce0b-45b9-80f7-dac07a0a721f"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("cbff4804-2176-45ee-ba5d-767ab03c0322"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("185a63b8-4c5b-41e3-979b-491cf5046d4c"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("1b815e3f-a1a0-42a3-9256-878257eb77cb"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("c14dd7b3-f2bb-4edf-9035-20347b5fabbc"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("d7fd0f25-dd73-4a3c-ae24-2846e32275d2"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("f685d765-233e-4a0f-ab96-7285eb88d069"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("0f14c298-9718-417e-b80d-13f880ebc713"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("c4bea8a5-ce0b-45b9-80f7-dac07a0a721f"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("cbff4804-2176-45ee-ba5d-767ab03c0322"));

            migrationBuilder.DropColumn(
                name: "FruitBlastLifetimeExplosions",
                table: "PlayerProfiles");

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("22aa7380-e596-43bf-9e58-e1db17cc52bb"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("37b67d61-c310-45eb-b133-7e43e1c847d5"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("593880d2-d2f0-485f-84e3-198f6dbed3b5"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("e54c597b-1189-444c-8ee6-5187fad2e8d7"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("f1de42a9-d3c1-4def-a93e-576e3a961b3d"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6708));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6711));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6709));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6705));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6703));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 3, 12, 19, 24, 41, 177, DateTimeKind.Utc).AddTicks(6707));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("23e39603-f644-4de3-b3bc-7a109c40862c"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("5ce1bf67-8bf1-4898-808b-5dd1428ad465"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" },
                    { new Guid("a56f526b-d91b-41dc-a483-d68ed8887029"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" }
                });
        }
    }
}
