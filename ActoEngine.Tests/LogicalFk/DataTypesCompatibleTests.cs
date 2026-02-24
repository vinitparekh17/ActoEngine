using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.LogicalFk;

/// <summary>
/// Tests for <see cref="DataTypeCompatibility"/> — ensures the type-family
/// grouping (integer, string, guid) survives refactors.
/// </summary>
public class DataTypesCompatibleTests
{
    #region Integer Family

    [Theory]
    [InlineData("int", "INT")]
    [InlineData("int", "bigint")]
    [InlineData("smallint", "tinyint")]
    [InlineData("BIGINT", "int")]
    public void IntegerFamily_AreCompatible(string source, string target)
    {
        Assert.True(DataTypeCompatibility.AreCompatible(source, target));
    }

    #endregion

    #region String Family

    [Theory]
    [InlineData("varchar", "nvarchar")]
    [InlineData("nchar", "char")]
    [InlineData("VARCHAR", "nvarchar")]
    public void StringFamily_AreCompatible(string source, string target)
    {
        Assert.True(DataTypeCompatibility.AreCompatible(source, target));
    }

    #endregion

    #region GUID

    [Fact]
    public void UniqueIdentifier_MatchesSelf()
    {
        Assert.True(DataTypeCompatibility.AreCompatible("uniqueidentifier", "uniqueidentifier"));
    }

    #endregion

    #region Cross-Family Mismatches

    [Theory]
    [InlineData("varchar", "int")]
    [InlineData("int", "uniqueidentifier")]
    [InlineData("nvarchar", "bigint")]
    [InlineData("decimal", "int")]
    public void CrossFamily_AreIncompatible(string source, string target)
    {
        Assert.False(DataTypeCompatibility.AreCompatible(source, target));
    }

    #endregion

    #region Whitespace and Case Handling

    [Theory]
    [InlineData("  INT  ", "int")]
    [InlineData("int", "  BIGINT  ")]
    [InlineData("  VARCHAR  ", "nvarchar")]
    public void WhitespaceAndCase_Normalized(string source, string target)
    {
        Assert.True(DataTypeCompatibility.AreCompatible(source, target));
    }

    #endregion

    #region Unmapped Types — Exact Match Required

    [Theory]
    [InlineData("decimal", "decimal")]
    [InlineData("money", "money")]
    [InlineData("bit", "bit")]
    public void UnmappedTypes_MatchSelf(string type1, string type2)
    {
        Assert.True(DataTypeCompatibility.AreCompatible(type1, type2));
    }

    [Theory]
    [InlineData("decimal", "money")]
    [InlineData("float", "real")]
    public void UnmappedTypes_DontCrossMatch(string type1, string type2)
    {
        Assert.False(DataTypeCompatibility.AreCompatible(type1, type2));
    }

    #endregion
}
