using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIModelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModelConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BaseUrlOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    SupportsVision = table.Column<bool>(type: "bit", nullable: false),
                    LastHealthCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHealthOk = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModelConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIModelConfigs_Priority",
                table: "AIModelConfigs",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIModelConfigs");
        }
    }
}
