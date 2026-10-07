using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheGate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DirectTradeRecords_Status",
                table: "DirectTradeRecords");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BuyerClosedAtUtc",
                table: "DirectTradeRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProducerClosedAtUtc",
                table: "DirectTradeRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComplianceTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequirementSource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceTasks", x => x.Id);
                    table.CheckConstraint("CK_ComplianceTasks_Status", "\"Status\" IN ('Required', 'EvidenceRecorded', 'ResponsiblePartyAttested') AND ((\"Status\" = 'Required' AND \"EvidenceReference\" IS NULL) OR (\"Status\" IN ('EvidenceRecorded', 'ResponsiblePartyAttested') AND \"EvidenceReference\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ComplianceTasks_DirectTradeRecords_TradeRecordId",
                        column: x => x.TradeRecordId,
                        principalTable: "DirectTradeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogisticsShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleProviderOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastMilestone = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsShipments", x => x.Id);
                    table.CheckConstraint("CK_LogisticsShipments_LastMilestone", "\"LastMilestone\" >= 0 AND \"LastMilestone\" <= 10");
                    table.ForeignKey(
                        name: "FK_LogisticsShipments_DirectTradeRecords_TradeRecordId",
                        column: x => x.TradeRecordId,
                        principalTable: "DirectTradeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentPartnerOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PartnerReportedSettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentObligations", x => x.Id);
                    table.CheckConstraint("CK_PaymentObligations_Status", "\"Amount\" > 0 AND \"Status\" IN ('Pending', 'PartnerReportedSettled') AND ((\"Status\" = 'Pending' AND \"PartnerReportedSettledAtUtc\" IS NULL) OR (\"Status\" = 'PartnerReportedSettled' AND \"PartnerReportedSettledAtUtc\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_PaymentObligations_DirectTradeRecords_TradeRecordId",
                        column: x => x.TradeRecordId,
                        principalTable: "DirectTradeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogisticsMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsMilestones_LogisticsShipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "LogisticsShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DirectTradeRecords_Status",
                table: "DirectTradeRecords",
                sql: "\"Status\" IN ('AwaitingProducerConfirmation', 'Confirmed', 'Closed') AND ((\"Status\" = 'AwaitingProducerConfirmation' AND \"ProducerConfirmedAtUtc\" IS NULL) OR (\"Status\" IN ('Confirmed', 'Closed') AND \"ProducerConfirmedAtUtc\" IS NOT NULL)) AND ((\"Status\" = 'Closed' AND \"ProducerClosedAtUtc\" IS NOT NULL AND \"BuyerClosedAtUtc\" IS NOT NULL) OR (\"Status\" <> 'Closed' AND NOT (\"ProducerClosedAtUtc\" IS NOT NULL AND \"BuyerClosedAtUtc\" IS NOT NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceTasks_TradeRecordId",
                table: "ComplianceTasks",
                column: "TradeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsMilestones_ShipmentId_Type",
                table: "LogisticsMilestones",
                columns: new[] { "ShipmentId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsShipments_TradeRecordId_ShipmentReference",
                table: "LogisticsShipments",
                columns: new[] { "TradeRecordId", "ShipmentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligations_TradeRecordId",
                table: "PaymentObligations",
                column: "TradeRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceTasks");

            migrationBuilder.DropTable(
                name: "LogisticsMilestones");

            migrationBuilder.DropTable(
                name: "PaymentObligations");

            migrationBuilder.DropTable(
                name: "LogisticsShipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DirectTradeRecords_Status",
                table: "DirectTradeRecords");

            migrationBuilder.DropColumn(
                name: "BuyerClosedAtUtc",
                table: "DirectTradeRecords");

            migrationBuilder.DropColumn(
                name: "ProducerClosedAtUtc",
                table: "DirectTradeRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DirectTradeRecords_Status",
                table: "DirectTradeRecords",
                sql: "\"Status\" IN ('AwaitingProducerConfirmation', 'Confirmed')");
        }
    }
}
