using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Alerting.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchFloor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "watch_floor",
                schema: "alerting",
                table: "poll_cursor",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "watch_floor",
                schema: "alerting",
                table: "poll_cursor");
        }
    }
}
