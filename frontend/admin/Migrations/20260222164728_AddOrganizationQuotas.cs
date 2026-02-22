using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotebookAdmin.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationQuotas",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxNotebooks = table.Column<int>(type: "integer", nullable: false),
                    MaxEntriesPerNotebook = table.Column<int>(type: "integer", nullable: false),
                    MaxEntrySizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MaxTotalStorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationQuotas", x => x.OrganizationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationQuotas");
        }
    }
}
