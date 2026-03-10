using ActoEngine.WebApi.Features.Schema;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ActoEngine.Tests.Schema;

public class SchemaServiceTests
{
    private readonly ISchemaRepository _repositoryMock;
    private readonly ILogger<SchemaService> _loggerMock;
    private readonly SchemaService _schemaService;

    public SchemaServiceTests()
    {
        _repositoryMock = Substitute.For<ISchemaRepository>();
        _loggerMock = Substitute.For<ILogger<SchemaService>>();
        
        _schemaService = new SchemaService(_repositoryMock, _loggerMock);
    }

    [Fact]
    public void NormalizeAndHashDefinition_EmptyOrNull_ReturnsEmptyString()
    {
        // Act
        var hash1 = _schemaService.NormalizeAndHashDefinition("");
        var hash2 = _schemaService.NormalizeAndHashDefinition("   ");
        var hash3 = _schemaService.NormalizeAndHashDefinition(null!);

        // Assert
        Assert.Equal(string.Empty, hash1);
        Assert.Equal(string.Empty, hash2);
        Assert.Equal(string.Empty, hash3);
    }

    [Fact]
    public void NormalizeAndHashDefinition_SameContentDifferentCase_ReturnsSameHash()
    {
        // Arrange
        var def1 = "SELECT * FROM Users";
        var def2 = "select * from users";

        // Act
        var hash1 = _schemaService.NormalizeAndHashDefinition(def1);
        var hash2 = _schemaService.NormalizeAndHashDefinition(def2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void NormalizeAndHashDefinition_SameContentDifferentWhitespace_ReturnsSameHash()
    {
        // Arrange
        var def1 = "SELECT * FROM Users WHERE Id = 1";
        var def2 = "SELECT    * \r\n FROM \t Users    WHERE  Id  =  1";

        // Act
        var hash1 = _schemaService.NormalizeAndHashDefinition(def1);
        var hash2 = _schemaService.NormalizeAndHashDefinition(def2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void NormalizeAndHashDefinition_WithLineComments_IgnoresComments()
    {
        // Arrange
        var cleanDef = "SELECT * FROM Users";
        var commentedDef = @"SELECT * FROM Users
-- This is a comment
-- Another comment";
        var commentedInlineDef = "SELECT * FROM Users -- inline comment";

        // Act
        var cleanHash = _schemaService.NormalizeAndHashDefinition(cleanDef);
        var commentedHash = _schemaService.NormalizeAndHashDefinition(commentedDef);
        var inlineHash = _schemaService.NormalizeAndHashDefinition(commentedInlineDef);

        // Assert
        Assert.Equal(cleanHash, commentedHash);
        Assert.Equal(cleanHash, inlineHash);
    }

    [Fact]
    public void NormalizeAndHashDefinition_WithBlockComments_IgnoresComments()
    {
        // Arrange
        var cleanDef = "SELECT * FROM Users";
        var commentedDef = @"/* Block 
comment */
SELECT * /* inline */ FROM Users";

        // Act
        var cleanHash = _schemaService.NormalizeAndHashDefinition(cleanDef);
        var commentedHash = _schemaService.NormalizeAndHashDefinition(commentedDef);

        // Assert
        Assert.Equal(cleanHash, commentedHash);
    }

    [Fact]
    public void NormalizeAndHashDefinition_DifferentDefinitions_ReturnDifferentHashes()
    {
        // Arrange
        var def1 = "SELECT * FROM Users";
        var def2 = "SELECT Id FROM Users";

        // Act
        var hash1 = _schemaService.NormalizeAndHashDefinition(def1);
        var hash2 = _schemaService.NormalizeAndHashDefinition(def2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }
}
