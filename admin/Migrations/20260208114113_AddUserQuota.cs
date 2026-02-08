using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotebookAdmin.Migrations
{
    /// <inheritdoc />
    public partial class AddUserQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserQuotas",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    MaxNotebooks = table.Column<int>(type: "integer", nullable: false),
                    MaxEntriesPerNotebook = table.Column<int>(type: "integer", nullable: false),
                    MaxEntrySizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MaxTotalStorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuotas", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserQuotas_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserQuotas");
        }
    }
}
