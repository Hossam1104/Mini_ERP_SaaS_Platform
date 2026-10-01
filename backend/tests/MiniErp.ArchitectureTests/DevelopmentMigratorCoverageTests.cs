using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniErp.Infrastructure.Persistence;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class DevelopmentMigratorCoverageTests
{
    [Fact]
    public void DevelopmentMigrator_CoversEveryContextWithMigrations()
    {
        var assembly = typeof(DevelopmentSqlServerDatabaseMigrator).Assembly;
        var contextsWithMigrations = assembly.GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetCustomAttributes(typeof(DbContextAttribute), inherit: false)
                .Cast<DbContextAttribute>())
            .Select(attribute => attribute.ContextType)
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.Name)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var repositoryRoot = FindRepositoryRoot();
        var migratorPath = Path.Combine(
            repositoryRoot,
            "backend",
            "src",
            "MiniErp.Infrastructure",
            "Persistence",
            "DevelopmentSqlServerDatabaseMigrator.cs");
        var migratorSource = File.ReadAllText(migratorPath);
        var migratedContexts = Regex.Matches(migratorSource, @"new\s+(\w+DbContext)\s*\(")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = contextsWithMigrations.Except(migratedContexts, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(contextsWithMigrations);
        Assert.True(
            missing.Length == 0,
            $"The Development SQL Server migrator must cover every module-owned EF context with migrations. Missing: {string.Join(", ", missing)}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
