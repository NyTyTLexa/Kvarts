using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementSystem.Services.Retail.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialRetail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "retail");

        migrationBuilder.CreateTable(
            name: "RetailShops",
            schema: "retail",
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
            schema: "retail",
            table: "RetailShops",
            column: "Host",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RetailShops", schema: "retail");
    }
}
