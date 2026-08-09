using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Registry.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ai_consent",
                schema: "registry",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_consent",
                schema: "registry",
                table: "applications");
        }
    }
}
