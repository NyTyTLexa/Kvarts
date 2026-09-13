using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProcurementSystem.Infrastructure.Persistence;

#nullable disable

namespace ProcurementSystem.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260912120000_AddNotificationEmailedAt")]
public partial class AddNotificationEmailedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "EmailedAtUtc",
            table: "Notifications",
            type: "timestamp with time zone",
            nullable: true);

        // Уже накопленные уведомления считаем отправленными — иначе первый запуск
        // диспетчера разошлёт залп писем по давно прошедшим событиям.
        migrationBuilder.Sql("UPDATE \"Notifications\" SET \"EmailedAtUtc\" = \"CreatedAtUtc\";");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_EmailedAtUtc",
            table: "Notifications",
            column: "EmailedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Notifications_EmailedAtUtc",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "EmailedAtUtc",
            table: "Notifications");
    }
}
