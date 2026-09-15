using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Glaziovi.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.EnsureSchema(
                name: "persons");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "iam_users",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    external_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_profiles",
                schema: "persons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    first_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    last_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_profiles_users",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "iam_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_iam_users_external_subject",
                schema: "iam",
                table: "iam_users",
                column: "external_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_profiles_external_id",
                schema: "persons",
                table: "person_profiles",
                column: "external_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_profiles_user_id",
                schema: "persons",
                table: "person_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_profiles",
                schema: "persons");

            migrationBuilder.DropTable(
                name: "iam_users",
                schema: "iam");
        }
    }
}
