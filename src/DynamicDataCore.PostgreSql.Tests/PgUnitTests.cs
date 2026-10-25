using System.Reflection;
using DynamicDataCore.Abstractions.Procedures;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;

namespace DynamicDataCore.PostgreSql.Tests;

public sealed class PgUnitTests
{
    private static ProcedureName Name(string value) => ProcedureName.Create(value);

    [Fact]
    public void BuildText_uses_named_notation_for_each_kind()
    {
        var p = PgParameters.Create().Uuid("p_id", Guid.Empty).Json("p_lines", new[] { 1 });

        Assert.Equal("SELECT * FROM sales.fn_x(p_id => @p_id, p_lines => @p_lines)",
            PgRoutineExecutor.BuildText(PgRoutineExecutor.RoutineKind.SelectRows, Name("sales.fn_x"), p));
        Assert.Equal("SELECT sales.fn_x(p_id => @p_id, p_lines => @p_lines)",
            PgRoutineExecutor.BuildText(PgRoutineExecutor.RoutineKind.SelectScalar, Name("sales.fn_x"), p));
        Assert.Equal("CALL sales.sp_x(p_id => @p_id, p_lines => @p_lines)",
            PgRoutineExecutor.BuildText(PgRoutineExecutor.RoutineKind.Call, Name("sales.sp_x"), p));
    }

    [Fact]
    public void BuildText_without_parameters_has_empty_argument_list()
        => Assert.Equal("SELECT * FROM t.fn_items()",
            PgRoutineExecutor.BuildText(PgRoutineExecutor.RoutineKind.SelectRows, Name("t.fn_items"), null));

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("a; drop table x")]
    [InlineData("a => 1, b")]
    [InlineData("a)--")]
    public void Parameter_names_that_could_alter_the_statement_are_rejected(string name)
        => Assert.Throws<ArgumentException>(() => PgParameters.Create().Text(name, "x"));

    [Fact]
    public void Duplicate_parameter_names_are_rejected_ignoring_case_and_at_sign()
    {
        var p = PgParameters.Create().Text("p_name", "a");
        Assert.Throws<ArgumentException>(() => p.Text("@P_NAME", "b"));
    }

    [Fact]
    public void Leading_at_sign_is_stripped_from_parameter_names()
        => Assert.Equal("p_name", PgParameters.Create().Text("@p_name", "a").Items[0].Name);

    [Fact]
    public void Timestamp_forces_unspecified_kind()
    {
        var item = PgParameters.Create()
            .Timestamp("p_at", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Items[0];

        Assert.Equal(NpgsqlDbType.Timestamp, item.DbType);
        Assert.Equal(DateTimeKind.Unspecified, ((DateTime)item.Value!).Kind);
    }

    [Fact]
    public void Json_null_is_sent_as_null_jsonb()
    {
        var item = PgParameters.Create().Json("p", null).Items[0];

        Assert.Equal(NpgsqlDbType.Jsonb, item.DbType);
        Assert.Null(item.Value);
    }

    [Fact]
    public void Public_api_never_accepts_sql_text()
    {
        string[] forbidden = ["sql", "commandtext", "query", "statement", "text"];
        var types = new[] { typeof(IPgRoutineExecutor), typeof(PgRoutineExecutor), typeof(PgParameters) };

        var offenders = types
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters().Select(p => (Method: m, Parameter: p)))
            .Where(x => x.Parameter.ParameterType == typeof(string) && forbidden.Contains(x.Parameter.Name!.ToLowerInvariant()))
            // PgParameters.Text(name, value) takes a VALUE, not SQL; its parameters are 'name'/'value'.
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}({x.Parameter.Name})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AddDynamicDataCorePostgreSql_registers_scoped_executor()
    {
        var services = new ServiceCollection();
        services.AddDynamicDataCorePostgreSql();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPgRoutineExecutor));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(PgRoutineExecutor), descriptor.ImplementationType);
    }
}
