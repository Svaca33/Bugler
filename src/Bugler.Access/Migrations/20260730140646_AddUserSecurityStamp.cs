using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                schema: "access",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Accounts that predate the column get a stamp of their own rather than all sharing
            // the zero default: two Users must never carry the same stamp. Sessions minted before
            // this migration are refused whatever we write here — they carry no stamp at all.
            migrationBuilder.Sql("UPDATE access.users SET security_stamp = gen_random_uuid();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "access",
                table: "users");
        }
    }
}
