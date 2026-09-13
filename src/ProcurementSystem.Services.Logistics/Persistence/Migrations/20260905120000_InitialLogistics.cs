using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProcurementSystem.Services.Logistics.Persistence;

#nullable disable

namespace ProcurementSystem.Services.Logistics.Persistence.Migrations;

[DbContext(typeof(LogisticsDbContext))]
[Migration("20260905120000_InitialLogistics")]
public partial class InitialLogistics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "logistics");

        migrationBuilder.CreateTable(
            name: "GoodsReceipts",
            schema: "logistics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Customer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WarehouseRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                AccountingRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GoodsReceiptLines",
            schema: "logistics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                OrderedQty = table.Column<int>(type: "integer", nullable: false),
                ReceivedQty = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoodsReceiptLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId",
                    column: x => x.GoodsReceiptId,
                    principalSchema: "logistics",
                    principalTable: "GoodsReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "logistics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceiptLines_GoodsReceiptId",
            schema: "logistics",
            table: "GoodsReceiptLines",
            column: "GoodsReceiptId");

        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceipts_OrderId",
            schema: "logistics",
            table: "GoodsReceipts",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAtUtc",
            schema: "logistics",
            table: "OutboxMessages",
            column: "ProcessedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GoodsReceiptLines", schema: "logistics");
        migrationBuilder.DropTable(name: "OutboxMessages", schema: "logistics");
        migrationBuilder.DropTable(name: "GoodsReceipts", schema: "logistics");
    }
}
