using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLastActivityToSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("346433ce-c209-4177-a3ae-dddbe19a8298"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("3af79faf-841c-4104-9122-3f1e84330223"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("874fa363-f81b-447a-89aa-858603f6d709"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("d217ad4f-4ead-45a1-8fbb-56fd10a92c85"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("f8f9b8e5-cbfb-41a6-83b3-d028634fabaf"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("197f1e4c-bc80-4f2e-9dec-8913bc40f6b3"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("cd93a82c-0821-48c8-b9d0-5c0787a1bcaa"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("de4937c7-5cc3-46bb-8821-76ff7674abdc"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "GameSessions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("503b5049-4e43-455b-86bd-1dc4e44d6f78"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("560fd30e-b1e1-4008-9669-6c56214370ed"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("749820be-2e2c-4e87-abec-f76d72b5523e"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("77dda098-a4e2-4700-b35c-76a9302563a7"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("d51acb13-ebea-4256-9abe-ec3371babfd7"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4274));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4276));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4275));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4272));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4271));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 10, 12, 47, 3, 392, DateTimeKind.Utc).AddTicks(4273));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("29203408-3309-441a-a505-41dd9f044d92"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" },
                    { new Guid("2956800f-9666-4d67-8ca8-d3f8b5f5a6bb"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("bc4f2e29-3064-4150-97fe-557b751c30b8"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("503b5049-4e43-455b-86bd-1dc4e44d6f78"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("560fd30e-b1e1-4008-9669-6c56214370ed"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("749820be-2e2c-4e87-abec-f76d72b5523e"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("77dda098-a4e2-4700-b35c-76a9302563a7"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("d51acb13-ebea-4256-9abe-ec3371babfd7"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("29203408-3309-441a-a505-41dd9f044d92"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("2956800f-9666-4d67-8ca8-d3f8b5f5a6bb"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("bc4f2e29-3064-4150-97fe-557b751c30b8"));

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "GameSessions");

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("346433ce-c209-4177-a3ae-dddbe19a8298"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("3af79faf-841c-4104-9122-3f1e84330223"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("874fa363-f81b-447a-89aa-858603f6d709"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("d217ad4f-4ead-45a1-8fbb-56fd10a92c85"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("f8f9b8e5-cbfb-41a6-83b3-d028634fabaf"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6688));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6690));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6689));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6686));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6685));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 9, 2, 33, 23, 268, DateTimeKind.Utc).AddTicks(6687));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("197f1e4c-bc80-4f2e-9dec-8913bc40f6b3"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("cd93a82c-0821-48c8-b9d0-5c0787a1bcaa"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" },
                    { new Guid("de4937c7-5cc3-46bb-8821-76ff7674abdc"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" }
                });
        }
    }
}
