using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.LogicalFk;

/// <summary>
/// Tests for <see cref="TableNameResolver"/> — pluralisation, case-insensitive
/// matching, schema-qualified and bracketed name handling.
/// </summary>
public class TableResolutionTests
{
    /// <summary>Helper to create a case-insensitive table name lookup.</summary>
    private static Dictionary<string, int> Tables(params (string Name, int Id)[] tables)
        => tables.ToDictionary(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase);

    #region TryResolveTable — Pluralization

    [Fact]
    public void TryResolve_DirectMatch()
    {
        var tables = Tables(("Customer", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("Customer", tables));
    }

    [Fact]
    public void TryResolve_CaseInsensitive()
    {
        var tables = Tables(("Customers", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("customers", tables));
    }

    [Fact]
    public void TryResolve_AddS_Pluralization()
    {
        // "customer" → "customers"
        var tables = Tables(("Customers", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("customer", tables));
    }

    [Fact]
    public void TryResolve_AddES_Pluralization()
    {
        // "box" → "boxes"
        var tables = Tables(("Boxes", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("box", tables));
    }

    [Fact]
    public void TryResolve_StripS_Singularization()
    {
        // "orders" → "Order"
        var tables = Tables(("Order", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("orders", tables));
    }

    [Fact]
    public void TryResolve_YtoIES_Pluralization()
    {
        // "category" → "categories"
        var tables = Tables(("Categories", 1));
        Assert.Equal(1, TableNameResolver.TryResolveTable("category", tables));
    }

    [Fact]
    public void TryResolve_NoMatch_ReturnsNull()
    {
        var tables = Tables(("Users", 1));
        Assert.Null(TableNameResolver.TryResolveTable("xyz", tables));
    }

    [Fact]
    public void TryResolve_DirectMatch_Preferred_OverPlural()
    {
        // If both "Customer" and "Customers" exist, direct match wins
        var tables = Tables(("Customer", 1), ("Customers", 2));
        Assert.Equal(1, TableNameResolver.TryResolveTable("Customer", tables));
    }

    #endregion

    #region ResolveTableName — Schema-Qualified Names

    [Fact]
    public void Resolve_DirectMatch()
    {
        var tables = Tables(("Orders", 1));
        Assert.Equal(1, TableNameResolver.ResolveTableName("Orders", tables));
    }

    [Fact]
    public void Resolve_SchemaQualified()
    {
        // "dbo.Orders" → strip "dbo." → "Orders"
        var tables = Tables(("Orders", 1));
        Assert.Equal(1, TableNameResolver.ResolveTableName("dbo.Orders", tables));
    }

    [Fact]
    public void Resolve_BracketedSchemaQualified()
    {
        // "[dbo].[Orders]" → strip brackets and schema
        var tables = Tables(("Orders", 1));
        Assert.Equal(1, TableNameResolver.ResolveTableName("[dbo].[Orders]", tables));
    }

    [Fact]
    public void Resolve_Bracketed_NoSchema()
    {
        var tables = Tables(("Orders", 1));
        Assert.Equal(1, TableNameResolver.ResolveTableName("[Orders]", tables));
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNull()
    {
        var tables = Tables(("Users", 1));
        Assert.Null(TableNameResolver.ResolveTableName("NonExistent", tables));
    }

    #endregion
}
