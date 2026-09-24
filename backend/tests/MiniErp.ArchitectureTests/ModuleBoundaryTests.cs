using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Platform;
using MiniErp.Contracts.Modules.Platform;
using MiniErp.Infrastructure.Persistence;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly ContractsAssembly = typeof(IPlatformAdministrationModule).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PlatformModuleRegistration).Assembly;
    private static readonly Assembly ApiAssembly = Assembly.Load("MiniErp.Api");
    private static readonly Assembly InfrastructureAssembly = typeof(TenantPersistenceSessionFactory).Assembly;

    [Fact]
    public void Contracts_do_not_reference_application()
    {
        Assert.DoesNotContain(
            ContractsAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApplicationAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_api()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApiAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Application_and_api_do_not_reference_entity_framework_core()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(
            ApiAssembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true);

        var repositoryRoot = FindRepositoryRoot();
        foreach (var project in new[] { "MiniErp.App", "MiniErp.Api" })
        {
            var projectFile = Path.Combine(repositoryRoot, "backend", "src", project, project + ".csproj");
            Assert.DoesNotContain(
                "EntityFrameworkCore",
                File.ReadAllText(projectFile),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, InfrastructureAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Contracts_do_not_reference_infrastructure()
    {
        Assert.DoesNotContain(
            ContractsAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, InfrastructureAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_reference_api()
    {
        Assert.DoesNotContain(
            InfrastructureAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApiAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Project_reference_direction_matches_adr_002()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.Equal(
            ["MiniErp.Contracts"],
            ProjectReferences(repositoryRoot, "MiniErp.App"));
        Assert.Equal(
            ["MiniErp.App", "MiniErp.Contracts"],
            ProjectReferences(repositoryRoot, "MiniErp.Infrastructure"));
        Assert.Equal(
            ["MiniErp.App", "MiniErp.Contracts", "MiniErp.Infrastructure"],
            ProjectReferences(repositoryRoot, "MiniErp.Api"));
        Assert.Equal(
            ["MiniErp.Api", "MiniErp.App", "MiniErp.Contracts", "MiniErp.Infrastructure"],
            ProjectReferences(repositoryRoot, "MiniErp.ArchitectureTests", testsProject: true));
    }

    [Fact]
    public void Privileged_boundary_is_not_resolvable_from_public_infrastructure_surface()
    {
        Assert.DoesNotContain(
            InfrastructureAssembly.GetExportedTypes(),
            type => type.Name.Contains("PrivilegedPersistenceBoundary", StringComparison.Ordinal));
    }

    [Fact]
    public void Platform_internal_implementation_is_not_public()
    {
        var implementationType = ApplicationAssembly.GetType(
            "MiniErp.App.Modules.Platform.Internal.PlatformAdministrationModule");

        Assert.NotNull(implementationType);
        Assert.False(implementationType!.IsPublic);
    }

    [Fact]
    public void Api_surface_does_not_expose_platform_internal_types()
    {
        var exposedInternalTypes = ApiAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith(
                "MiniErp.App.Modules.Platform.Internal",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(exposedInternalTypes);
    }

    [Fact]
    public void Composition_seam_returns_only_public_contract()
    {
        var module = PlatformModuleRegistration.Create();

        Assert.IsAssignableFrom<IPlatformAdministrationModule>(module);
        Assert.Equal("platform-administration", module.Descriptor.Key);
        Assert.True(module.RegistrationEvidence.IsRegistered);
    }

    [Fact]
    public void Infrastructure_public_surface_has_no_unscoped_ef_shapes()
    {
        foreach (var type in InfrastructureAssembly.GetExportedTypes())
        {
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(constructor.GetParameters(), parameter =>
                    IsUnscopedEfShape(parameter.ParameterType)
                    || IsTenantOverride(parameter)
                    || IsUnrestrictedPersistenceDelegate(parameter));
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    IsUnscopedEfShape(method.ReturnType),
                    $"{type.FullName}.{method.Name} exposes an unscoped EF return type.");
                Assert.DoesNotContain(method.GetParameters(), parameter =>
                    IsUnscopedEfShape(parameter.ParameterType)
                    || IsTenantOverride(parameter)
                    || IsUnrestrictedPersistenceDelegate(parameter));
            }
        }
    }

    [Fact]
    public void Tenant_persistence_registration_owns_the_options_builder()
    {
        var method = typeof(TenantPersistenceServiceCollectionExtensions)
            .GetMethod(nameof(TenantPersistenceServiceCollectionExtensions.AddTenantPersistence));

        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(DbContextOptions));
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(Action<DbContextOptionsBuilder>));
        Assert.DoesNotContain(
            typeof(TenantPersistenceSessionFactory).GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(DbContextOptions)));
    }

    [Fact]
    public void Infrastructure_unscoped_ef_operations_are_allow_listed_to_stored_owner_verifier()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(
            repositoryRoot,
            "backend",
            "src",
            "MiniErp.Infrastructure");
        var sourceUnits = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .Select(path => (
                RelativePath: NormalizePath(Path.GetRelativePath(repositoryRoot, path)),
                Source: File.ReadAllText(path)));
        var callSites = FindForbiddenEfInvocations(sourceUnits);

        AssertApprovedUnscopedCalls(callSites);
    }

    [Fact]
    public void All_backend_projects_are_scanned_for_forbidden_ef_invocations()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "backend", "src");
        var sourceUnits = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .Select(path => (
                RelativePath: NormalizePath(Path.GetRelativePath(repositoryRoot, path)),
                Source: File.ReadAllText(path)));
        var callSites = FindForbiddenEfInvocations(sourceUnits);

        AssertApprovedUnscopedCalls(callSites);
    }

    [Fact]
    public void Duplicate_verifier_filename_in_another_path_is_rejected()
    {
        var calls = FindForbiddenEfInvocations(
        [
            (ApprovedVerifierPath, "query.IgnoreQueryFilters();"),
            ("backend/src/Another.Persistence/Persistence/TenantOwnershipStoreVerifier.cs", "query.IgnoreQueryFilters();")
        ]);

        Assert.Contains(calls, callSite => !IsApprovedUnscopedCall(callSite));
    }

    [Fact]
    public void Every_forbidden_ef_api_is_detected_as_a_syntax_invocation()
    {
        var source = """
            query.IgnoreQueryFilters();
            EntityFrameworkQueryableExtensions.ExecuteUpdate(query, x => x.SetProperty(item => item.Id, item => item.Id));
            query.ExecuteUpdateAsync(x => x.SetProperty(item => item.Id, item => item.Id));
            EntityFrameworkQueryableExtensions.ExecuteDelete(query);
            query.ExecuteDeleteAsync();
            query.FromSql($"select 1");
            query.FromSqlRaw("select 1");
            query.FromSqlInterpolated($"select 1");
            context.Database.ExecuteSqlRaw("select 1");
            context.Database.ExecuteSqlRawAsync("select 1");
            context.Database.ExecuteSqlInterpolated($"select 1");
            context.Database.ExecuteSqlInterpolatedAsync($"select 1");
            context.Database.SqlQuery<int>($"select 1");
            context.Database.SqlQueryRaw<int>("select 1");
            """;

        var calls = FindForbiddenEfInvocations([(ApprovedVerifierPath, source)]);

        Assert.Equal(ForbiddenEfMethodNames.Count, calls.Count);
        Assert.Equal(
            ForbiddenEfMethodNames.OrderBy(name => name, StringComparer.Ordinal),
            calls.Select(callSite => callSite.Method).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Comments_and_strings_do_not_create_false_positive_ef_calls()
    {
        var source = """
            // query.IgnoreQueryFilters();
            var text = "query.ExecuteDeleteAsync()";
            query.AsNoTracking();
            """;

        Assert.Empty(FindForbiddenEfInvocations([(ApprovedVerifierPath, source)]));
    }

    private const string ApprovedVerifierPath =
        "backend/src/MiniErp.Infrastructure/Persistence/TenantOwnershipStoreVerifier.cs";
    private const string MigrationPersistencePath =
        "backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationPersistence.cs";
    private const string MigrationReconciliationPersistencePath =
        "backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationReconciliationPersistence.cs";

    private static readonly IReadOnlySet<string> ForbiddenEfMethodNames = new HashSet<string>(
    [
        "IgnoreQueryFilters",
        "ExecuteUpdate",
        "ExecuteUpdateAsync",
        "ExecuteDelete",
        "ExecuteDeleteAsync",
        "FromSql",
        "FromSqlRaw",
        "FromSqlInterpolated",
        "ExecuteSqlRaw",
        "ExecuteSqlRawAsync",
        "ExecuteSqlInterpolated",
        "ExecuteSqlInterpolatedAsync",
        "SqlQuery",
        "SqlQueryRaw"
    ],
    StringComparer.Ordinal);

    private sealed record ForbiddenEfInvocation(string RelativePath, string Method, int Line);

    private static IReadOnlyList<ForbiddenEfInvocation> FindForbiddenEfInvocations(
        IEnumerable<(string RelativePath, string Source)> sourceUnits)
    {
        var calls = new List<ForbiddenEfInvocation>();
        foreach (var sourceUnit in sourceUnits)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceUnit.Source, path: sourceUnit.RelativePath);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var method = ResolveInvokedMethodName(invocation);
                if (method is null || !ForbiddenEfMethodNames.Contains(method))
                {
                    continue;
                }

                var line = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                calls.Add(new ForbiddenEfInvocation(
                    NormalizePath(sourceUnit.RelativePath),
                    method,
                    line));
            }
        }

        return calls;
    }

    private static string? ResolveInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                _ => null
            },
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsApprovedUnscopedCall(ForbiddenEfInvocation callSite)
    {
        return callSite.RelativePath == ApprovedVerifierPath && callSite.Method == "IgnoreQueryFilters"
            || (callSite.RelativePath == MigrationPersistencePath || callSite.RelativePath == MigrationReconciliationPersistencePath)
                && callSite.Method == "ExecuteSqlInterpolatedAsync";
    }

    private static void AssertApprovedUnscopedCalls(IReadOnlyList<ForbiddenEfInvocation> callSites)
    {
        Assert.Equal(4, callSites.Count);
        Assert.Equal(1, callSites.Count(callSite => callSite.RelativePath == ApprovedVerifierPath && callSite.Method == "IgnoreQueryFilters"));
        Assert.Equal(1, callSites.Count(callSite => callSite.RelativePath == MigrationPersistencePath && callSite.Method == "ExecuteSqlInterpolatedAsync"));
        Assert.Equal(2, callSites.Count(callSite => callSite.RelativePath == MigrationReconciliationPersistencePath && callSite.Method == "ExecuteSqlInterpolatedAsync"));
        Assert.DoesNotContain(callSites, callSite => !IsApprovedUnscopedCall(callSite));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static bool IsUnscopedEfShape(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            return IsUnscopedEfShape(type.GetElementType()!);
        }

        if (type == typeof(DbContext)
            || type == typeof(IQueryable))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(DbSet<>) || definition == typeof(IQueryable<>))
            {
                return true;
            }

            return type.GetGenericArguments().Any(IsUnscopedEfShape);
        }

        return false;
    }

    private static bool IsTenantOverride(ParameterInfo parameter)
    {
        return ContainsTenantIdType(parameter.ParameterType)
            || (parameter.ParameterType == typeof(bool)
                && (parameter.Name?.Contains("ignore", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("filter", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("unscoped", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("bypass", StringComparison.OrdinalIgnoreCase) == true));
    }

    private static bool ContainsTenantIdType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            return ContainsTenantIdType(type.GetElementType()!);
        }

        if (type == typeof(TenantId) || Nullable.GetUnderlyingType(type) == typeof(TenantId))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsTenantIdType);
    }

    private static bool IsUnrestrictedPersistenceDelegate(ParameterInfo parameter)
    {
        if (!typeof(Delegate).IsAssignableFrom(parameter.ParameterType))
        {
            return false;
        }

        return parameter.ParameterType != typeof(Action<DbContextOptionsBuilder>);
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

        throw new DirectoryNotFoundException("Repository root could not be located for architecture source checks.");
    }

    private static string[] ProjectReferences(
        string repositoryRoot,
        string project,
        bool testsProject = false)
    {
        var projectPath = testsProject
            ? Path.Combine(repositoryRoot, "backend", "tests", project, project + ".csproj")
            : Path.Combine(repositoryRoot, "backend", "src", project, project + ".csproj");
        var document = XDocument.Load(projectPath);
        return document
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                (string?)reference.Attribute("Include") ?? string.Empty))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void Known_project_dependency_graph_has_no_cycle()
    {
        var assemblies = new[] { ContractsAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly };
        var names = assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly);
        var graph = assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(referenceName => referenceName is not null && names.ContainsKey(referenceName))
                .Cast<string>()
                .ToArray());

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names.Keys)
        {
            Assert.False(HasCycle(name, graph, visited, active));
        }
    }

    private static bool HasCycle(
        string name,
        IReadOnlyDictionary<string, string[]> graph,
        ISet<string> visited,
        ISet<string> active)
    {
        if (active.Contains(name))
        {
            return true;
        }

        if (!visited.Add(name))
        {
            return false;
        }

        active.Add(name);
        foreach (var dependency in graph[name])
        {
            if (HasCycle(dependency, graph, visited, active))
            {
                return true;
            }
        }

        active.Remove(name);
        return false;
    }
    // Architecture ratchets R2-R5 (docs/ARCHITECTURE.md § Enforcement). Exception lists are
    // shrink-only: remove an entry when the code no longer needs it; never add one to go green.

    private static readonly IReadOnlySet<string> AllowedAppModuleEdges = new HashSet<string>(
    [
        "BusinessParties->MasterData", // two-way with MasterData->BusinessParties
        "Finance->Identity",
        "Finance->Inventory",
        "Finance->Sales", // two-way with Sales->Finance
        "Inventory->MasterData",
        "Inventory->Procurement",
        "Inventory->Sales", // two-way with Sales->Inventory
        "MasterData->Audit",
        "MasterData->BusinessParties",
        "Migration->Audit",
        "Migration->Finance",
        "Migration->Inventory",
        "Notifications->Audit",
        "Procurement->BusinessParties",
        "Procurement->MasterData",
        "Reporting->Audit",
        "Reporting->Finance",
        "Reporting->Inventory",
        "Reporting->Procurement",
        "Reporting->Sales",
        "Sales->BusinessParties",
        "Sales->Finance",
        "Sales->Inventory",
        "Sales->MasterData",
        "Sales->Procurement"
    ],
    StringComparer.Ordinal);

    [Fact]
    public void App_module_edges_match_the_frozen_shrink_only_allowlist()
    {
        var modulesRoot = Path.Combine(FindRepositoryRoot(), "backend", "src", "MiniErp.App", "Modules");
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var moduleDirectory in Directory.GetDirectories(modulesRoot))
        {
            var module = Path.GetFileName(moduleDirectory);
            foreach (var path in SourceFiles(moduleDirectory))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"MiniErp\.App\.Modules\.(\w+)"))
                {
                    if (match.Groups[1].Value != module)
                    {
                        actual.Add($"{module}->{match.Groups[1].Value}");
                    }
                }
            }
        }

        Assert.Empty(actual.Except(AllowedAppModuleEdges)); // a new cross-module edge
        Assert.Empty(AllowedAppModuleEdges.Except(actual)); // an edge was removed: strike it from the list
    }

    [Fact]
    public void Module_persistence_does_not_reference_another_modules_persistence()
    {
        var persistenceRoot = Path.Combine(FindRepositoryRoot(), "backend", "src", "MiniErp.Infrastructure", "Persistence", "Modules");
        var modules = Directory.GetDirectories(persistenceRoot).Select(path => Path.GetFileName(path)).ToArray();
        var violations = new List<string>();
        foreach (var module in modules)
        {
            foreach (var path in SourceFiles(Path.Combine(persistenceRoot, module)))
            {
                var source = File.ReadAllText(path);
                violations.AddRange(modules
                    .Where(other => other != module
                        && Regex.IsMatch(source, $@"\bPersistence\.Modules\.{other}\b|\b{other}DbContext\b"))
                    .Select(other => $"{module}/{Path.GetFileName(path)} -> {other}"));
            }
        }

        Assert.Equal(7, modules.Length);
        Assert.Empty(violations);
    }

    [Fact]
    public void Allow_listed_raw_sql_is_tenant_scoped_and_lock_only()
    {
        var repositoryRoot = FindRepositoryRoot();
        var statements = new[] { MigrationPersistencePath, MigrationReconciliationPersistencePath }
            .SelectMany(relativePath => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repositoryRoot, relativePath)))
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => ResolveInvokedMethodName(invocation) == "ExecuteSqlInterpolatedAsync")
                .Select(invocation => invocation.ArgumentList.Arguments[0].Expression.ToString()))
            .ToArray();

        Assert.Equal(3, statements.Length);
        Assert.All(statements, sql =>
        {
            Assert.StartsWith("$\"SELECT ", sql, StringComparison.Ordinal);
            Assert.Contains("WITH (UPDLOCK", sql, StringComparison.Ordinal);
            Assert.Contains("[TenantId] = {", sql, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sql_server_safety_collection_classes_keep_the_hosted_ci_exclusion_suffix()
    {
        // Hosted CI skips LocalDB tests with --filter "FullyQualifiedName!~SqlServerSafetyTests".
        var collectionMembers = typeof(ModuleBoundaryTests).Assembly.GetTypes()
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType == typeof(CollectionAttribute)
                && attribute.ConstructorArguments.Count == 1
                && Equals(attribute.ConstructorArguments[0].Value, SqlServerSafetyCollection.Name)))
            .ToArray();

        Assert.NotEmpty(collectionMembers);
        Assert.Empty(collectionMembers
            .Where(type => !type.Name.EndsWith("SqlServerSafetyTests", StringComparison.Ordinal))
            .Select(type => type.FullName));
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));
}
