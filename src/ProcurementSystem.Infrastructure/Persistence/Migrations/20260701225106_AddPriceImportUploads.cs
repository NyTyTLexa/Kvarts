using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceImportUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceImportUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RowsProcessed = table.Column<int>(type: "integer", nullable: false),
                    ProductsCreated = table.Column<int>(type: "integer", nullable: false),
                    OffersUpserted = table.Column<int>(type: "integer", nullable: false),
                    ErrorsCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorsSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceImportUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceImportUploads_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuoteOverrides",
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
                        principalTable: "SpecificationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceImportUploads_CreatedAtUtc",
                table: "PriceImportUploads",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PriceImportUploads_VendorId",
                table: "PriceImportUploads",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOverrides_SpecificationItemId",
                table: "QuoteOverrides",
                column: "SpecificationItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceImportUploads");

            migrationBuilder.DropTable(
                name: "QuoteOverrides");
        }
    }
}
