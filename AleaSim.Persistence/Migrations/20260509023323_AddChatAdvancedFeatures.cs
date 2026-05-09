using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAdvancedFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("57ed17a2-2c8e-49e9-905b-d9022bfabe76"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("909a4d1b-275b-417e-aacc-c4afa2a54e24"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("9326ce25-e5fc-494d-9226-595fa6f5d9eb"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("d5ad5c23-2437-4335-877f-a49a02747f6e"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("fc77ae7b-8205-4577-ab79-9619f830f402"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("864b0b6b-ebb0-446e-8820-caf0370903bd"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("c3952dc6-37d3-437d-b360-7ad33d4530cc"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("cf426720-5966-4284-a6d3-9486454be464"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatMessages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "ChatMessages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "ChatMessages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "ChatMessages");

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("57ed17a2-2c8e-49e9-905b-d9022bfabe76"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("909a4d1b-275b-417e-aacc-c4afa2a54e24"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m },
                    { new Guid("9326ce25-e5fc-494d-9226-595fa6f5d9eb"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("d5ad5c23-2437-4335-877f-a49a02747f6e"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("fc77ae7b-8205-4577-ab79-9619f830f402"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3133));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3135));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3134));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3130));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3129));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 5, 4, 14, 24, 30, 551, DateTimeKind.Utc).AddTicks(3131));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("864b0b6b-ebb0-446e-8820-caf0370903bd"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" },
                    { new Guid("c3952dc6-37d3-437d-b360-7ad33d4530cc"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" },
                    { new Guid("cf426720-5966-4284-a6d3-9486454be464"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" }
                });
        }
    }
}
