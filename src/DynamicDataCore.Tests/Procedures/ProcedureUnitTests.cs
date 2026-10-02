using System.Data;
using System.Reflection;
using DynamicDataCore.Abstractions;
using DynamicDataCore.Abstractions.Procedures;
using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Infrastructure.Extensions;
using DynamicDataCore.Infrastructure.Procedures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Tests.Procedures;

public class ProcedureNameTests
{
    [Theory]
    [InlineData("dbo.usp_GetOrders")]
    [InlineData("_s._p")]
    [InlineData("Sales.p1")]
    public void Create_AcceptsSchemaDotName(string value)
    {
        var name = ProcedureName.Create(value);
        Assert.Equal(value, name.FullName);
        Assert.Equal(value, name.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("usp_GetOrders")]                 // schema is mandatory
    [InlineData("a.b.c")]                         // three parts
    [InlineData("[dbo].[usp]")]                   // brackets
    [InlineData("dbo.usp; DROP TABLE x")]         // injection attempt
    [InlineData("dbo.usp--")]
    [InlineData("dbo.usp ")]
    [InlineData("dbo.usp\n")]                     // '$' would accept this; \z must not
    [InlineData("1dbo.usp")]
    [InlineData("dbo.1usp")]
    [InlineData("dbo.usp()")]
    [InlineData("dbo.ñandú")]                     // non-ASCII word chars
    [InlineData("dbo.")]
    [InlineData(".usp")]
    public void Create_RejectsAnythingElse(string value)
    {
        Assert.Throws<ArgumentException>(() => ProcedureName.Create(value));
        Assert.False(ProcedureName.TryCreate(value, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void Create_RejectsNullAndOverlongParts()
    {
        Assert.Throws<ArgumentNullException>(() => ProcedureName.Create(null!));
        Assert.False(ProcedureName.TryCreate(null, out _));
        Assert.False(ProcedureName.TryCreate("dbo." + new string('a', 129), out _));
        Assert.True(ProcedureName.TryCreate("dbo." + new string('a', 128), out _));
    }

    [Fact]
    public void TwoPartOverload_AppliesSameValidation()
    {
        Assert.Equal("dbo.x", ProcedureName.Create("dbo", "x").FullName);
        Assert.Throws<ArgumentException>(() => ProcedureName.Create("dbo.x", "y"));
    }

    [Fact]
    public void Equality_IsByValue()
        => Assert.Equal(ProcedureName.Create("dbo.x"), ProcedureName.Create("dbo.x"));

    [Fact]
    public void TableTypeName_SharesTheSameRules()
    {
        Assert.Equal("ddc.IdList", TableTypeName.Create("ddc.IdList").FullName);
        Assert.Throws<ArgumentException>(() => TableTypeName.Create("IdList"));
        Assert.Throws<ArgumentException>(() => TableTypeName.Create("ddc.IdList; --"));
    }
}

public class ProcedureParametersTests
{
    [Fact]
    public void Builder_CapturesInputOutputReturnAndNormalizesNames()
    {
        var p = ProcedureParameters.Create()
            .Input("Name", SqlDbType.NVarChar, "x", size: 50)
            .Input("@Amount", SqlDbType.Decimal, 1.5m, precision: 18, scale: 2)
            .Output("@Count", SqlDbType.Int)
            .InputOutput("@Msg", SqlDbType.NVarChar, "a", size: 100)
            .ReturnValue();

        Assert.Equal(5, p.Items.Count);
        Assert.Equal("@Name", p.Items[0].Name);
        Assert.Equal(50, p.Items[0].Size);
        Assert.Equal((byte)18, p.Items[1].Precision);
        Assert.Equal((byte)2, p.Items[1].Scale);
        Assert.Equal(ProcedureParameterDirection.Output, p.Items[2].Direction);
        Assert.Equal(ProcedureParameterDirection.InputOutput, p.Items[3].Direction);
        Assert.Equal(ProcedureParameterDirection.ReturnValue, p.Items[4].Direction);
        Assert.Equal("@ReturnValue", p.Items[4].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("@a b")]
    [InlineData("@a;drop")]
    [InlineData("@1a")]
    [InlineData("@a\n")]
    public void Builder_RejectsBadNames(string name)
        => Assert.Throws<ArgumentException>(() => ProcedureParameters.Create().Input(name, SqlDbType.Int, 1));

    [Fact]
    public void Builder_RejectsDuplicatesAndSecondReturnValue()
    {
        var p = ProcedureParameters.Create().Input("@a", SqlDbType.Int, 1).ReturnValue();
        Assert.Throws<ArgumentException>(() => p.Input("@A", SqlDbType.Int, 2));
        Assert.Throws<InvalidOperationException>(() => p.ReturnValue("@other"));
    }

    [Fact]
    public void Builder_RequiresSizeForVariableLengthOutput()
    {
        Assert.Throws<ArgumentException>(() => ProcedureParameters.Create().Output("@m", SqlDbType.NVarChar));
        ProcedureParameters.Create().Output("@m", SqlDbType.NVarChar, size: -1);
    }

    [Fact]
    public void Builder_RejectsInvalidSizeAndMisplacedPrecision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcedureParameters.Create().Input("@a", SqlDbType.NVarChar, "x", size: 0));
        Assert.Throws<ArgumentException>(() => ProcedureParameters.Create().Input("@a", SqlDbType.Int, 1, precision: 5));
    }

    [Fact]
    public void Structured_AcceptsDataTableAndRows_ButNotThroughInput()
    {
        var type = TableTypeName.Create("ddc.IdList");
        var table = new DataTable();
        var rows = new TableValuedRows([new TableValueColumn("Id", SqlDbType.Int)], [[1], [2]]);

        var p = ProcedureParameters.Create().Structured("@t", table, type).Structured("@r", rows, type);

        Assert.All(p.Items, i => Assert.Equal(SqlDbType.Structured, i.DbType));
        Assert.Equal("ddc.IdList", p.Items[0].TableType!.FullName);
        Assert.Throws<ArgumentException>(() => ProcedureParameters.Create().Input("@x", SqlDbType.Structured, table));
    }

    [Fact]
    public void TableValuedRows_ValidatesColumns()
    {
        Assert.Throws<ArgumentException>(() => new TableValuedRows([], []));
        Assert.Throws<ArgumentException>(() => new TableValuedRows([new TableValueColumn("a b", SqlDbType.Int)], []));
    }

    [Fact]
    public void ProcedureResult_GetOutput_ConvertsAndHandlesNull()
    {
        var r = new ProcedureResult
        {
            RowsAffected = 1,
            OutputValues = new Dictionary<string, object?> { ["@n"] = 5, ["@z"] = null }
        };
        Assert.Equal(5L, r.GetOutput<long>("n"));
        Assert.Null(r.GetOutput<int?>("@z"));
        Assert.Throws<KeyNotFoundException>(() => r.GetOutput<int>("@missing"));
    }
}

public class ProcedureRegistrationTests
{
    [Fact]
    public void AddDynamicDataCoreProcedures_ResolvesExecutorFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Main"] = "Server=unused;Database=unused" })
            .Build();
        var sp = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddDynamicDataCoreProcedures("Main")
            .BuildServiceProvider();

        Assert.IsType<SqlProcedureExecutor>(sp.GetRequiredService<ISqlProcedureExecutor>());
    }

    [Fact]
    public void AddDynamicDataCoreProcedures_ThrowsWhenConnectionStringMissing_WithoutLeakingValues()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddDynamicDataCoreProcedures("Nope")
            .BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<ISqlProcedureExecutor>());
        Assert.Contains("Nope", ex.Message);
    }

    [Fact]
    public void AddDynamicDataCoreProcedures_RejectsBlankName()
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddDynamicDataCoreProcedures(" "));

    [Fact]
    public void AddDynamicDataCoreReadRepositories_RegistersOpenGeneric()
    {
        var services = new ServiceCollection().AddDynamicDataCoreReadRepositories();
        Assert.Contains(services, d => d.ServiceType == typeof(IReadRepository<>));
    }
}

/// <summary>
/// Reflection lock: no public member of the published assemblies may take free-form SQL text.
/// Complements the BannedApiAnalyzers rule (BannedSymbols.txt).
/// </summary>
public class NoRawSqlSurfaceTests
{
    private static readonly string[] ForbiddenNames = ["sql", "commandtext", "query"];

    private static readonly Assembly[] Published =
    [
        typeof(IUnitOfWork).Assembly,            // DynamicDataCore.Abstractions
        typeof(OffsetPageRequest).Assembly,      // DynamicDataCore.Common
        typeof(SqlProcedureExecutor).Assembly    // DynamicDataCore.Infrastructure (package "DynamicDataCore")
    ];

    [Fact]
    public void PublishedAssemblies_AreThoseWeExpect()
        => Assert.Equal(
            ["DynamicDataCore.Abstractions", "DynamicDataCore.Common", "DynamicDataCore.Infrastructure"],
            Published.Select(a => a.GetName().Name!).Order());

    [Fact]
    public void NoPublicMethodOrConstructor_HasStringParameterNamedSqlCommandTextOrQuery()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();

        foreach (var type in Published.SelectMany(a => a.GetExportedTypes()))
        {
            var members = type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags));
            foreach (var m in members)
            {
                if (!(m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly)) continue; // visible to consumers
                foreach (var p in m.GetParameters())
                {
                    if (p.ParameterType == typeof(string) && p.Name is not null
                        && ForbiddenNames.Contains(p.Name.ToLowerInvariant()))
                        offenders.Add($"{type.FullName}.{m.Name}({p.Name})");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Executor_Surface_OnlyAcceptsProcedureNameAsTheTarget()
    {
        foreach (var m in typeof(ISqlProcedureExecutor).GetMethods())
        {
            var first = m.GetParameters()[0];
            Assert.Equal(typeof(ProcedureName), first.ParameterType);
            Assert.DoesNotContain(m.GetParameters(), p => p.ParameterType == typeof(string));
            Assert.Equal(typeof(CancellationToken), m.GetParameters()[^1].ParameterType);
        }
    }
}
