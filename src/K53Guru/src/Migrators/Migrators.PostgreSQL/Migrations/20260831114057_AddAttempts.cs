using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace K53Guru.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learner_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_modified_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    learner_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_modified_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_attempts_learner_profiles_learner_profile_id",
                        column: x => x.learner_profile_id,
                        principalTable: "learner_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attempt_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    attempt_id = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    section = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    stem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sign_ref = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_modified_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempt_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_attempt_questions_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attempt_answer_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    attempt_question_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_modified_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempt_answer_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_attempt_answer_options_attempt_questions_attempt_question_id",
                        column: x => x.attempt_question_id,
                        principalTable: "attempt_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attempt_answer_options_attempt_question_id",
                table: "attempt_answer_options",
                column: "attempt_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempt_questions_attempt_id",
                table: "attempt_questions",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempts_learner_profile_id",
                table: "attempts",
                column: "learner_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempt_answer_options");

            migrationBuilder.DropTable(
                name: "attempt_questions");

            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropTable(
                name: "learner_profiles");
        }
    }
}
