using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheGate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTradeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeclaredQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    MinimumDirectTradeQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AllocatedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalContactUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOffers", x => x.Id);
                    table.CheckConstraint("CK_ProductOffers_Quantity", "CAST(\"DeclaredQuantity\" AS NUMERIC) > 0 AND CAST(\"MinimumDirectTradeQuantity\" AS NUMERIC) > 0 AND CAST(\"MinimumDirectTradeQuantity\" AS NUMERIC) <= CAST(\"DeclaredQuantity\" AS NUMERIC) AND CAST(\"AllocatedQuantity\" AS NUMERIC) >= 0 AND CAST(\"AllocatedQuantity\" AS NUMERIC) <= CAST(\"DeclaredQuantity\" AS NUMERIC)");
                });

            migrationBuilder.CreateTable(
                name: "DirectTradeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectTradeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectTradeRecords_ProductOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "ProductOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifierOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasuredQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    InspectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationReports_DirectTradeRecords_TradeRecordId",
                        column: x => x.TradeRecordId,
                        principalTable: "DirectTradeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectTradeRecords_BuyerOrganizationId",
                table: "DirectTradeRecords",
                column: "BuyerOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectTradeRecords_OfferId",
                table: "DirectTradeRecords",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectTradeRecords_ProducerOrganizationId",
                table: "DirectTradeRecords",
                column: "ProducerOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOffers_ProducerOrganizationId",
                table: "ProductOffers",
                column: "ProducerOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationReports_TradeRecordId",
                table: "VerificationReports",
                column: "TradeRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationReports");

            migrationBuilder.DropTable(
                name: "DirectTradeRecords");

            migrationBuilder.DropTable(
                name: "ProductOffers");
        }
    }
}
