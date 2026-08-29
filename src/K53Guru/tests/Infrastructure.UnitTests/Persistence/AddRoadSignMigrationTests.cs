using K53Guru.Migrators.PostgreSQL.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Covers spec-1-1-seed-road-sign-catalog.md I/O &amp; Edge-Case Matrix row:
///   - "Migration applied": fresh PostgreSQL database -> RoadSigns table exists with the correct
///     schema including the unique index on legislation_code.
///
/// No live PostgreSQL server is available in this environment, so this test invokes the actual
/// generated AddRoadSign migration's Up() method (via reflection, since Migration.Up is
/// protected) and asserts on the resulting DDL operations - i.e. it verifies the exact schema the
/// shipped migration would create against a real database, without needing one.
/// </summary>
public class AddRoadSignMigrationTests
{
    private static MigrationOperation[] GetUpOperations()
    {
        var migration = new AddRoadSign();
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");

        var upMethod = typeof(AddRoadSign).GetMethod("Up",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(upMethod);
        upMethod!.Invoke(migration, new object[] { migrationBuilder });

        return migrationBuilder.Operations.ToArray();
    }

    [Fact]
    public void Up_CreatesRoadSignsTable_WithLegislationCodeDescriptionAndImageAssetKeyColumns()
    {
        var operations = GetUpOperations();

        var createTable = operations
            .OfType<CreateTableOperation>()
            .SingleOrDefault(op => op.Name == "road_signs");

        Assert.NotNull(createTable);

        var columnNames = createTable!.Columns.Select(c => c.Name).ToArray();
        Assert.Contains("legislation_code", columnNames);
        Assert.Contains("description", columnNames);
        Assert.Contains("image_asset_key", columnNames);

        var legislationCodeColumn = createTable.Columns.Single(c => c.Name == "legislation_code");
        Assert.False(legislationCodeColumn.IsNullable);

        var descriptionColumn = createTable.Columns.Single(c => c.Name == "description");
        Assert.False(descriptionColumn.IsNullable);

        var imageAssetKeyColumn = createTable.Columns.Single(c => c.Name == "image_asset_key");
        Assert.True(imageAssetKeyColumn.IsNullable);
    }

    [Fact]
    public void Up_CreatesUniqueIndex_OnLegislationCode()
    {
        var operations = GetUpOperations();

        var index = operations
            .OfType<CreateIndexOperation>()
            .SingleOrDefault(op => op.Table == "road_signs" && op.Columns.Contains("legislation_code"));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }
}
