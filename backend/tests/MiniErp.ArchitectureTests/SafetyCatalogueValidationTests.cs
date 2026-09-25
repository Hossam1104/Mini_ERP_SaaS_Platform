using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Structurally validates the Foundation safety assertion matrix in
/// docs/history/96_Foundation_Release1_Safety_Validation.md so the catalogue cannot
/// silently drift into duplicate/missing row numbers, an unrecognized status
/// value, or a Passed row with no evidence text.
/// </summary>
public sealed class SafetyCatalogueValidationTests
{
    private static readonly string[] AllowedStatuses = { "PASS", "NOT APPLICABLE", "DEFERRED" };

    private sealed record CatalogueRow(int Number, string Assertion, string Evidence, string Status, string DeferredOwnerOrGate);

    [Fact]
    public void Catalogue_rows_are_uniquely_and_sequentially_numbered_from_one()
    {
        var rows = ReadCatalogueRows();

        Assert.NotEmpty(rows);
        var numbers = rows.Select(r => r.Number).ToArray();
        Assert.Equal(numbers.Distinct().Count(), numbers.Length);

        var expected = Enumerable.Range(1, rows.Count).ToArray();
        Assert.Equal(expected, numbers.OrderBy(n => n));
    }

    [Fact]
    public void Catalogue_status_values_are_within_the_allowed_classification_vocabulary()
    {
        var rows = ReadCatalogueRows();

        foreach (var row in rows)
        {
            Assert.Contains(row.Status, AllowedStatuses);
        }
    }

    [Fact]
    public void Catalogue_rows_with_Passed_status_declare_non_empty_evidence()
    {
        var rows = ReadCatalogueRows();

        foreach (var row in rows.Where(r => r.Status == "PASS"))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(row.Evidence),
                $"Row {row.Number} is marked PASS but declares no evidence.");
            Assert.False(
                string.IsNullOrWhiteSpace(row.DeferredOwnerOrGate),
                $"Row {row.Number} is marked PASS but has no supporting checkpoint/owner note.");
        }
    }

    [Fact]
    public void Catalogue_rows_that_are_not_Passed_identify_an_owning_gate_or_scope_boundary()
    {
        var rows = ReadCatalogueRows();

        foreach (var row in rows.Where(r => r.Status != "PASS"))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(row.DeferredOwnerOrGate),
                $"Row {row.Number} is not PASS but names no deferred owner or scope boundary.");
        }
    }

    private static List<CatalogueRow> ReadCatalogueRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var path = Path.Combine(repositoryRoot, "docs", "history", "96_Foundation_Release1_Safety_Validation.md");
        Assert.True(File.Exists(path), $"Safety catalogue not found at {path}.");

        var rows = new List<CatalogueRow>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (!Regex.IsMatch(trimmed, @"^\|\s*\d+\s*\|"))
            {
                continue;
            }

            // Every row is delimited by exactly one leading and one trailing
            // '|'; trim those outer delimiters first so the split below
            // yields the real 5 table columns, not 5 columns plus two empty
            // tokens from the outer pipes.
            var cells = trimmed.Trim('|').Split('|');
            Assert.True(
                cells.Length == 5,
                $"Catalogue row starting '{trimmed[..Math.Min(40, trimmed.Length)]}' does not have exactly 5 columns.");

            var number = int.Parse(cells[0].Trim());
            rows.Add(new CatalogueRow(
                number,
                cells[1].Trim(),
                cells[2].Trim(),
                cells[3].Trim(),
                cells[4].Trim()));
        }

        return rows;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "backend",
                "src",
                "MiniErp.Infrastructure",
                "MiniErp.Infrastructure.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located for the safety catalogue check.");
    }
}
