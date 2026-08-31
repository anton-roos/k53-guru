using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K53Guru.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "explanation",
                table: "questions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "client_submitted_at",
                table: "attempts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "attempts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Test");

            migrationBuilder.AddColumn<string>(
                name: "explanation",
                table: "attempt_questions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "explanation",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "client_submitted_at",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "explanation",
                table: "attempt_questions");
        }
    }
}
