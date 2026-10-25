using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PinHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checkouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BorrowerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checkouts_People_BorrowerId",
                        column: x => x.BorrowerId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrackingMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    DamagedQuantity = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConditionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApproverId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_People_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckoutId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DecidedById = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckoutLines_Checkouts_CheckoutId",
                        column: x => x.CheckoutId,
                        principalTable: "Checkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckoutLines_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckoutLines_People_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: true),
                    CheckoutId = table.Column<int>(type: "int", nullable: true),
                    CheckoutLineId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrectsEventId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_CheckoutLines_CheckoutLineId",
                        column: x => x.CheckoutLineId,
                        principalTable: "CheckoutLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Checkouts_CheckoutId",
                        column: x => x.CheckoutId,
                        principalTable: "Checkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_HistoryEvents_CorrectsEventId",
                        column: x => x.CorrectsEventId,
                        principalTable: "HistoryEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_People_ActorId",
                        column: x => x.ActorId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutLines_CheckoutId",
                table: "CheckoutLines",
                column: "CheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutLines_DecidedById",
                table: "CheckoutLines",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutLines_EquipmentId",
                table: "CheckoutLines",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_BorrowerId",
                table: "Checkouts",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_ApproverId",
                table: "Equipment",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name",
                table: "Equipment",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_ActorId",
                table: "HistoryEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_AtUtc",
                table: "HistoryEvents",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_CheckoutId",
                table: "HistoryEvents",
                column: "CheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_CheckoutLineId",
                table: "HistoryEvents",
                column: "CheckoutLineId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_CorrectsEventId",
                table: "HistoryEvents",
                column: "CorrectsEventId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_EquipmentId",
                table: "HistoryEvents",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_People_Name",
                table: "People",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryEvents");

            migrationBuilder.DropTable(
                name: "CheckoutLines");

            migrationBuilder.DropTable(
                name: "Checkouts");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
