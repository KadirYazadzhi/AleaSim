using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AleaSim.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAuditLogsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CLEANUP (Issue 59, 55, 38 compatibility)
            migrationBuilder.Sql("DELETE FROM AuditLogs;");
            migrationBuilder.Sql("UPDATE GameRounds SET ShadowBrainResult = '{}' WHERE ShadowBrainResult = '';");
            migrationBuilder.Sql("UPDATE GameRounds SET RandomResult = '{}' WHERE RandomResult = '';");
            
            // Handle existing columns safely via manual SQL check (MariaDB/Old MySQL compatibility)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'GameSessions';
                SET @columnname = 'TotalWagered';
                SET @preparedStatement = (SELECT IF(
                  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                  CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname),
                  'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'GameSessions';
                SET @columnname = 'TotalWon';
                SET @preparedStatement = (SELECT IF(
                  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
                  CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname),
                  'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

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

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualRtp",
                table: "PlayerProfiles",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalWagered",
                table: "GameSessions",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalWon",
                table: "GameSessions",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "ShadowBrainResult",
                table: "GameRounds",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RandomResult",
                table: "GameRounds",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Category", "ConditionType", "ConditionValue", "Description", "Icon", "Name", "RewardAmount" },
                values: new object[,]
                {
                    { new Guid("2c9ed706-29cb-434f-b15d-88c91fc25120"), "General", "LevelReached", 10m, "Reach Level 10", "🎖️", "Veteran", 0m },
                    { new Guid("44a4c45e-acdc-4d7c-9123-0178fd012822"), "General", "MaxMultiplier", 100m, "Hit a win over 100x multiplier", "⭐", "Lucky Star", 0m },
                    { new Guid("91759d86-7df0-4112-b7d5-5c552e374cb1"), "General", "TotalWagered", 5000m, "Wager more than $5,000 total", "💎", "High Roller", 0m },
                    { new Guid("a7df386c-8f12-4cf5-a01a-5fe92a88dff7"), "General", "TotalBets", 1m, "Place your first bet", "🎯", "First Blood", 0m },
                    { new Guid("df78193b-f041-4dbc-b8cd-c951fdb7bbbe"), "General", "TotalWagered", 50000m, "Wager more than $50,000 total", "🐋", "The Whale", 0m }
                });

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Help",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(264));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Privacy",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(266));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "Content_Terms",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(265));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "EmergencyStop",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(262));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "GlobalTargetRtp",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(260));

            migrationBuilder.UpdateData(
                table: "GlobalSettings",
                keyColumn: "Key",
                keyValue: "VolatilityMode",
                column: "LastUpdated",
                value: new DateTime(2026, 4, 11, 18, 2, 7, 355, DateTimeKind.Utc).AddTicks(263));

            migrationBuilder.InsertData(
                table: "Quests",
                columns: new[] { "Id", "Description", "GoalType", "IsActive", "RewardAmount", "TargetValue", "Title" },
                values: new object[,]
                {
                    { new Guid("472f5fea-a5d4-40c6-91bd-2a56e0e866ac"), "Complete 50 spins on any slot", "SpinCount", true, 10m, 50m, "Daily Spinner" },
                    { new Guid("4feca67d-f018-4ce6-a022-6620d413229b"), "Wager a total of $1,000", "TotalWager", true, 50m, 1000m, "High Stakes" },
                    { new Guid("5e8f010a-b9c8-48df-abbb-66ace205e5b5"), "Win a total of $500", "WinAmount", true, 25m, 500m, "Big Win Hunter" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs");

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("2c9ed706-29cb-434f-b15d-88c91fc25120"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("44a4c45e-acdc-4d7c-9123-0178fd012822"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("91759d86-7df0-4112-b7d5-5c552e374cb1"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("a7df386c-8f12-4cf5-a01a-5fe92a88dff7"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("df78193b-f041-4dbc-b8cd-c951fdb7bbbe"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("472f5fea-a5d4-40c6-91bd-2a56e0e866ac"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("4feca67d-f018-4ce6-a022-6620d413229b"));

            migrationBuilder.DeleteData(
                table: "Quests",
                keyColumn: "Id",
                keyValue: new Guid("5e8f010a-b9c8-48df-abbb-66ace205e5b5"));

            migrationBuilder.DropColumn(
                name: "TotalWagered",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "TotalWon",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<double>(
                name: "ActualRtp",
                table: "PlayerProfiles",
                type: "double",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<string>(
                name: "ShadowBrainResult",
                table: "GameRounds",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RandomResult",
                table: "GameRounds",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "AuditLogs",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

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
    }
}
