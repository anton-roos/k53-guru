using K53Guru.Migrators.PostgreSQL.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-4-author-combination-signs.md's Verification row for the
/// AddRoadSignComponentCodes migration, mirroring AddRoadSignMigrationTests.cs's approach for
/// AddRoadSign: no live PostgreSQL server is available in this environment, so this test invokes
/// the actual generated migration's Up()/Down() methods (via reflection, since Migration.Up/Down
/// are protected) and asserts on the resulting DDL operations - i.e. it verifies the exact schema
/// the shipped, checked-in migration script would create/revert against a real database, not just
/// an EnsureCreated()-derived schema.
/// </summary>
public class AddRoadSignComponentCodesMigrationTests
{
    private static MigrationOperation[] GetUpOperations()
    {
        var migration = new AddRoadSignComponentCodes();
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");

        var upMethod = typeof(AddRoadSignComponentCodes).GetMethod("Up",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(upMethod);
        upMethod!.Invoke(migration, new object[] { migrationBuilder });

        return migrationBuilder.Operations.ToArray();
    }

    private static MigrationOperation[] GetDownOperations()
    {
        var migration = new AddRoadSignComponentCodes();
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");

        var downMethod = typeof(AddRoadSignComponentCodes).GetMethod("Down",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(downMethod);
        downMethod!.Invoke(migration, new object[] { migrationBuilder });

        return migrationBuilder.Operations.ToArray();
    }

    [Fact]
    public void Up_AddsComponentSignCodesColumn_NullableWithMaxLength200()
    {
        var operations = GetUpOperations();

        var addColumn = operations
            .OfType<AddColumnOperation>()
            .SingleOrDefault(op => op.Table == "road_signs" && op.Name == "component_sign_codes");

        Assert.NotNull(addColumn);
        Assert.True(addColumn!.IsNullable);
        Assert.Equal(typeof(string), addColumn.ClrType);
        Assert.Equal(200, addColumn.MaxLength);
    }

    [Fact]
    public void Down_DropsComponentSignCodesColumn()
    {
        var operations = GetDownOperations();

        var dropColumn = operations
            .OfType<DropColumnOperation>()
            .SingleOrDefault(op => op.Table == "road_signs" && op.Name == "component_sign_codes");

        Assert.NotNull(dropColumn);
    }
}
