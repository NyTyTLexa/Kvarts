using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementSystem.Services.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "notifications");

        migrationBuilder.CreateTable(
            name: "Notifications",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                RecipientRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                RelatedEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                DedupeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EmailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_DedupeKey",
            schema: "notifications",
            table: "Notifications",
            column: "DedupeKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_EmailedAtUtc",
            schema: "notifications",
            table: "Notifications",
            column: "EmailedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_RecipientRole_ReadAtUtc_CreatedAtUtc",
            schema: "notifications",
            table: "Notifications",
            columns: new[] { "RecipientRole", "ReadAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_RecipientUserName_ReadAtUtc_CreatedAtUtc",
            schema: "notifications",
            table: "Notifications",
            columns: new[] { "RecipientUserName", "ReadAtUtc", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Notifications", schema: "notifications");
    }
}
