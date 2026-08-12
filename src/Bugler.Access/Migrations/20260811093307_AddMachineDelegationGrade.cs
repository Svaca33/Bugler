using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineDelegationGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every delegation issued before grades existed could only read — that is what the
            // grade named Reading is, so existing rows get it rather than an unnamed zero.
            migrationBuilder.AddColumn<short>(
                name: "grade",
                schema: "access",
                table: "machine_delegations",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grade",
                schema: "access",
                table: "machine_delegations");
        }
    }
}
