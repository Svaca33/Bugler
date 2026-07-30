using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Registry.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "trace_retention_days",
                schema: "registry",
                table: "services",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_retention_days",
                schema: "registry",
                table: "services");
        }
    }
}
