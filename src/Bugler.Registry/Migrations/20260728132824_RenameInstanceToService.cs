using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bugler.Registry.Migrations
{
    /// <summary>
    /// Instance becomes Service (ADR 0006). Written as a rename rather than the scaffolded
    /// drop-and-create so existing registrations and their API keys survive: the old free-form
    /// instance name stays as the Service Name, and the two new facets are backfilled with a
    /// placeholder — the split of "demo-prod" into namespace and environment cannot be inferred.
    /// </summary>
    public partial class RenameInstanceToService : Migration
    {
        private const string BackfilledFacet = "unknown";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "instances",
                schema: "registry",
                newName: "services");

            migrationBuilder.Sql("""
                ALTER TABLE registry.services RENAME CONSTRAINT pk_instances TO pk_services;
                ALTER TABLE registry.services
                    RENAME CONSTRAINT fk_instances_applications_application_id
                    TO fk_services_applications_application_id;
                """);

            migrationBuilder.RenameColumn(
                name: "instance_id",
                schema: "registry",
                table: "api_keys",
                newName: "service_id");

            migrationBuilder.RenameIndex(
                name: "ix_api_keys_instance_id",
                schema: "registry",
                table: "api_keys",
                newName: "ix_api_keys_service_id");

            migrationBuilder.Sql("""
                ALTER TABLE registry.api_keys
                    RENAME CONSTRAINT fk_api_keys_instances_instance_id
                    TO fk_api_keys_services_service_id;
                """);

            migrationBuilder.DropIndex(
                name: "ix_instances_application_id_name",
                schema: "registry",
                table: "services");

            migrationBuilder.AddColumn<string>(
                name: "namespace",
                schema: "registry",
                table: "services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "environment",
                schema: "registry",
                table: "services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                $"UPDATE registry.services SET namespace = '{BackfilledFacet}', environment = '{BackfilledFacet}'");

            migrationBuilder.AlterColumn<string>(
                name: "namespace",
                schema: "registry",
                table: "services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "environment",
                schema: "registry",
                table: "services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_services_application_id_namespace_environment_name",
                schema: "registry",
                table: "services",
                columns: new[] { "application_id", "namespace", "environment", "name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_services_application_id_namespace_environment_name",
                schema: "registry",
                table: "services");

            migrationBuilder.DropColumn(name: "environment", schema: "registry", table: "services");
            migrationBuilder.DropColumn(name: "namespace", schema: "registry", table: "services");

            migrationBuilder.Sql("""
                ALTER TABLE registry.api_keys
                    RENAME CONSTRAINT fk_api_keys_services_service_id
                    TO fk_api_keys_instances_instance_id;
                """);

            migrationBuilder.RenameIndex(
                name: "ix_api_keys_service_id",
                schema: "registry",
                table: "api_keys",
                newName: "ix_api_keys_instance_id");

            migrationBuilder.RenameColumn(
                name: "service_id",
                schema: "registry",
                table: "api_keys",
                newName: "instance_id");

            migrationBuilder.Sql("""
                ALTER TABLE registry.services
                    RENAME CONSTRAINT fk_services_applications_application_id
                    TO fk_instances_applications_application_id;
                ALTER TABLE registry.services RENAME CONSTRAINT pk_services TO pk_instances;
                """);

            migrationBuilder.RenameTable(
                name: "services",
                schema: "registry",
                newName: "instances");

            migrationBuilder.CreateIndex(
                name: "ix_instances_application_id_name",
                schema: "registry",
                table: "instances",
                columns: new[] { "application_id", "name" },
                unique: true);
        }
    }
}
