using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K53Guru.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeResultUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_code_results_attempt_id",
                table: "code_results");

            migrationBuilder.CreateIndex(
                name: "ix_code_results_attempt_id_code",
                table: "code_results",
                columns: new[] { "attempt_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_code_results_attempt_id_code",
                table: "code_results");

            migrationBuilder.CreateIndex(
                name: "ix_code_results_attempt_id",
                table: "code_results",
                column: "attempt_id");
        }
    }
}
