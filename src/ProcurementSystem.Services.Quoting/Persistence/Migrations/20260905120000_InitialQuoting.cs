using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementSystem.Services.Quoting.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialQuoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quoting");

            migrationBuilder.CreateTable(
                name: "Specifications",
                schema: "quoting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Customer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationItems",
                schema: "quoting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RawName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecificationItems_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalSchema: "quoting",
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuoteOverrides",
                schema: "quoting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteOverrides_SpecificationItems_SpecificationItemId",
                        column: x => x.SpecificationItemId,
                        principalSchema: "quoting",
                        principalTable: "SpecificationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationItems_SpecificationId",
                schema: "quoting",
                table: "SpecificationItems",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOverrides_SpecificationItemId",
                schema: "quoting",
                table: "QuoteOverrides",
                column: "SpecificationItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteOverrides",
                schema: "quoting");

            migrationBuilder.DropTable(
                name: "SpecificationItems",
                schema: "quoting");

            migrationBuilder.DropTable(
                name: "Specifications",
                schema: "quoting");
        }
    }
}
