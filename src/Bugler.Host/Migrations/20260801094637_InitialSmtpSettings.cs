using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Host.Migrations
{
    /// <inheritdoc />
    public partial class InitialSmtpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "server");

            migrationBuilder.CreateTable(
                name: "smtp_settings",
                schema: "server",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    host = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    security = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    username = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    from = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_smtp_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "smtp_settings",
                schema: "server");
        }
    }
}
