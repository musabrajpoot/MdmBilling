using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MdmBilling.Migrations
{
    /// <inheritdoc />
    public partial class BatchServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatchServers",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    ServerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchServers", x => new { x.BatchId, x.ServerId });
                    table.ForeignKey(
                        name: "FK_BatchServers_BillingBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "BillingBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchServers_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchServers_ServerId",
                table: "BatchServers",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchServers");
        }
    }
}
