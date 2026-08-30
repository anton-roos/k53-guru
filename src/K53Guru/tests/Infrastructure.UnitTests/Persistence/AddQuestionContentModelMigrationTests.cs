using K53Guru.Migrators.PostgreSQL.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-3-question-content-model.md I/O &amp; Edge-Case Matrix row:
///   - "Migration applied": fresh PostgreSQL database -> Questions/AnswerOptions tables exist
///     with correct schema and FK.
///
/// No live PostgreSQL server is available in this environment, so this test invokes the actual
/// generated AddQuestionContentModel migration's Up() method (via reflection, since Migration.Up
/// is protected) and asserts on the resulting DDL operations - i.e. it verifies the exact schema
/// the shipped migration would create against a real database, without needing one. Mirrors
/// Story 1.1's AddRoadSignMigrationTests.cs.
/// </summary>
public class AddQuestionContentModelMigrationTests
{
    private static MigrationOperation[] GetUpOperations()
    {
        var migration = new AddQuestionContentModel();
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");

        var upMethod = typeof(AddQuestionContentModel).GetMethod("Up",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(upMethod);
        upMethod!.Invoke(migration, new object[] { migrationBuilder });

        return migrationBuilder.Operations.ToArray();
    }

    private static CreateTableOperation GetCreateTableOperation(MigrationOperation[] operations, string tableName)
    {
        var createTable = operations
            .OfType<CreateTableOperation>()
            .SingleOrDefault(op => op.Name == tableName);

        Assert.NotNull(createTable);
        return createTable!;
    }

    [Fact]
    public void Up_CreatesQuestionsTable_WithStemCodesSectionLanguageCodeAndSignRefColumns()
    {
        var operations = GetUpOperations();
        var createTable = GetCreateTableOperation(operations, "questions");

        var columnNames = createTable.Columns.Select(c => c.Name).ToArray();
        Assert.Contains("stem", columnNames);
        Assert.Contains("codes", columnNames);
        Assert.Contains("section", columnNames);
        Assert.Contains("language_code", columnNames);
        Assert.Contains("sign_ref", columnNames);

        Assert.False(createTable.Columns.Single(c => c.Name == "stem").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "codes").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "section").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "language_code").IsNullable);

        // sign_ref is nullable - it is never an FK and a Rules-section question may have none.
        Assert.True(createTable.Columns.Single(c => c.Name == "sign_ref").IsNullable);
    }

    [Fact]
    public void Up_CreatesAnswerOptionsTable_WithQuestionIdTextIsCorrectAndOrderColumns()
    {
        var operations = GetUpOperations();
        var createTable = GetCreateTableOperation(operations, "answer_options");

        var columnNames = createTable.Columns.Select(c => c.Name).ToArray();
        Assert.Contains("question_id", columnNames);
        Assert.Contains("text", columnNames);
        Assert.Contains("is_correct", columnNames);
        Assert.Contains("order", columnNames);

        Assert.False(createTable.Columns.Single(c => c.Name == "question_id").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "text").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "is_correct").IsNullable);
        Assert.False(createTable.Columns.Single(c => c.Name == "order").IsNullable);
    }

    [Fact]
    public void Up_CreatesForeignKey_FromAnswerOptionsQuestionIdToQuestionsId()
    {
        var operations = GetUpOperations();
        var createTable = GetCreateTableOperation(operations, "answer_options");

        var foreignKey = createTable.ForeignKeys
            .SingleOrDefault(fk => fk.PrincipalTable == "questions" && fk.Columns.Contains("question_id"));

        Assert.NotNull(foreignKey);
        Assert.Equal(new[] { "question_id" }, foreignKey!.Columns);
        Assert.Equal(new[] { "id" }, foreignKey.PrincipalColumns);
    }
}
