using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProcurementSystem.Infrastructure.Persistence;

#nullable disable

namespace ProcurementSystem.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904180000_AddRetailShops")]
public partial class AddRetailShops : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceUrl",
            table: "PriceListItems",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RetailShops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SearchUrlTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                FirstSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSuccessUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                HitCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RetailShops", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RetailShops_Host",
            table: "RetailShops",
            column: "Host",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RetailShops");
        migrationBuilder.DropColumn(name: "SourceUrl", table: "PriceListItems");
    }
}
