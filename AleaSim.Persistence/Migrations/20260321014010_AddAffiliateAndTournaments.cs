using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateAndTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredById",
                table: "Users",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "SystemErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Message = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StackTrace = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Path = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemErrors", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PrizePool = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GameTypesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemErrors");

            migrationBuilder.DropTable(
                name: "Tournaments");

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

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferredById",
                table: "Users");

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
    }
}
