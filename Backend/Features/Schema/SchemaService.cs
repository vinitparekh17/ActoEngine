using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Schema;

public interface ISchemaService
{
    Task<List<string>> GetAllTablesAsync(string connectionString);

    Task<List<(string TableName, string SchemaName)>> GetAllTablesWithSchemaAsync(string connectionString);
    Task<TableSchemaResponse> GetTableSchemaAsync(string connectionString, string tableName);
    Task<List<DatabaseTableInfo>> GetDatabaseStructureAsync(string connectionString);
    Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString);
    Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(string connectionString, IEnumerable<string> tableNames);
    Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(string connectionString, IEnumerable<(string SchemaName, string TableName)> tables);

    // Entity Resync & Diff Core Utilities
    Task<IEnumerable<dynamic>> GetStoredProcedureModifyDatesAsync(string connectionString);
    string NormalizeAndHashDefinition(string definition);
    Task<List<SpHashInfo>> GetSpHashesAsync(int projectId);
    Task<bool> UpdateSpDefinitionAndHashAsync(
        int projectId,
        int spId,
        string definition,
        string definitionHash,
        DateTime sourceModifyDate,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null);
    Task<bool> SoftDeleteTableAsync(int projectId, int tableId);
    Task<bool> SoftDeleteSpAsync(int projectId, int spId);

    // Methods for stored metadata
    // Lightweight list methods (minimal bandwidth)
    Task<List<TableListDto>> GetTablesListAsync(int projectId);
    Task<List<StoredProcedureListDto>> GetStoredProceduresListAsync(int projectId);

    // Full metadata methods
    Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId);
    Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId);
    Task<List<StoredProcedureMetadataDto>> GetStoredProceduresMetadataAsync(int projectId);
    Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName, string schemaName);

    // Tree structure
    Task<TreeNode> GetDatabaseTreeAsync(int projectId, string databaseName);

    // Methods to retrieve individual entities by ID
    Task<TableMetadataDto?> GetTableByIdAsync(int tableId);
    Task<ColumnMetadataDto?> GetColumnByIdAsync(int columnId);
    Task<StoredProcedureMetadataDto?> GetSpByIdAsync(int spId);
}

public partial class SchemaService(
    ISchemaRepository schemaRepository,
    ILogger<SchemaService> logger) : ISchemaService
{
    private readonly ISchemaRepository _schemaRepository = schemaRepository;
    private readonly ILogger<SchemaService> _logger = logger;

    public async Task<List<string>> GetAllTablesAsync(string connectionString)
    {
        try
        {
            return await _schemaRepository.GetAllTablesAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tables from database");
            throw;
        }
    }

    public async Task<List<(string TableName, string SchemaName)>> GetAllTablesWithSchemaAsync(string connectionString)
    {
        try
        {
            var tablesWithSchema = await _schemaRepository.GetAllTablesWithSchemaAsync(connectionString);

            return tablesWithSchema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tables with schema from database");
            throw;
        }
    }

    public async Task<TableSchemaResponse> GetTableSchemaAsync(string connectionString, string tableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            return await _schemaRepository.ReadTableSchemaAsync(connectionString, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for table: {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<DatabaseTableInfo>> GetDatabaseStructureAsync(string connectionString)
    {
        try
        {
            var tables = await _schemaRepository.GetAllTablesAsync(connectionString);

            var databaseStructure = new List<DatabaseTableInfo>();

            foreach (var table in tables)
            {
                var parts = table.Split('.');
                if (parts.Length == 2)
                {
                    databaseStructure.Add(new DatabaseTableInfo
                    {
                        SchemaName = parts[0],
                        TableName = parts[1]
                    });
                }
                else
                {
                    databaseStructure.Add(new DatabaseTableInfo
                    {
                        SchemaName = "dbo",
                        TableName = table
                    });
                }
            }

            return [.. databaseStructure.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database structure");
            throw;
        }
    }

    public async Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(string connectionString)
    {
        try
        {
            return await _schemaRepository.GetStoredProceduresAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedures from database");
            throw;
        }
    }

    public async Task<IEnumerable<dynamic>> GetStoredProcedureModifyDatesAsync(string connectionString)
    {
        try
        {
            return await _schemaRepository.GetStoredProcedureModifyDatesAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedure modify dates from database");
            throw;
        }
    }

    public string NormalizeAndHashDefinition(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition)) return string.Empty;

        var normalized = NormalizeSqlOutsideLiterals(definition);

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<List<SpHashInfo>> GetSpHashesAsync(int projectId)
    {
        return await _schemaRepository.GetSpHashesAsync(projectId);
    }

    public async Task<bool> UpdateSpDefinitionAndHashAsync(
        int projectId,
        int spId,
        string definition,
        string definitionHash,
        DateTime sourceModifyDate,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null)
    {
        return await _schemaRepository.UpdateSpDefinitionAndHashAsync(
            projectId,
            spId,
            definition,
            definitionHash,
            sourceModifyDate,
            connection,
            transaction);
    }

    public async Task<bool> SoftDeleteTableAsync(int projectId, int tableId)
    {
        return await _schemaRepository.SoftDeleteTableAsync(projectId, tableId);
    }

    public async Task<bool> SoftDeleteSpAsync(int projectId, int spId)
    {
        return await _schemaRepository.SoftDeleteSpAsync(projectId, spId);
    }

    // Lightweight list methods
    public async Task<List<TableListDto>> GetTablesListAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetTablesListAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tables list for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<StoredProcedureListDto>> GetStoredProceduresListAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetStoredProceduresListAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedures list for project {ProjectId}", projectId);
            throw;
        }
    }

    // Full metadata methods
    public async Task<List<TableMetadataDto>> GetStoredTablesAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetStoredTablesAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored tables for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<ColumnMetadataDto>> GetStoredColumnsAsync(int tableId)
    {
        try
        {
            return await _schemaRepository.GetStoredColumnsAsync(tableId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored columns for table {TableId}", tableId);
            throw;
        }
    }

    public async Task<List<StoredProcedureMetadataDto>> GetStoredProceduresMetadataAsync(int projectId)
    {
        try
        {
            return await _schemaRepository.GetStoredStoredProceduresAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedure metadata for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<TableSchemaResponse> GetStoredTableSchemaAsync(int projectId, string tableName, string schemaName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            if (string.IsNullOrWhiteSpace(schemaName))
            {
                throw new ArgumentException("Schema name cannot be null or empty", nameof(schemaName));
            }

            return await _schemaRepository.GetStoredTableSchemaAsync(projectId, tableName, schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored table schema for table {SchemaName}.{TableName} in project {ProjectId}", schemaName, tableName, projectId);
            throw;
        }
    }

    public async Task<TreeNode> GetDatabaseTreeAsync(int projectId, string databaseName)
    {
        try
        {
            var tables = await GetStoredTablesAsync(projectId);
            var procedures = await GetStoredProceduresMetadataAsync(projectId);

            var tablesChildren = new List<TreeNode>();
            foreach (var table in tables.OrderBy(t => t.TableName))
            {
                // Get columns for this table
                var columns = await GetStoredColumnsAsync(table.TableId);

                var columnNodes = columns
                    .OrderBy(c => c.ColumnOrder)
                    .Select(col => new TreeNode
                    {
                        Id = $"col-{table.TableId}-{col.ColumnId}",
                        Name = col.ColumnName,
                        Type = "column"
                    })
                    .ToList();

                tablesChildren.Add(new TreeNode
                {
                    Id = $"table-{table.TableId}",
                    Name = table.TableName,
                    Type = "table",
                    Children = columnNodes
                });
            }

            var tablesFolder = new TreeNode
            {
                Id = $"db-{projectId}-tables",
                Name = "Tables",
                Type = "tables-folder",
                Children = tablesChildren
            };

            // Build Stored Procedures folder
            var spChildren = procedures
                .OrderBy(sp => sp.ProcedureName)
                .Select(sp => new TreeNode
                {
                    Id = $"sp-{sp.SpId}",
                    Name = sp.ProcedureName,
                    Type = "stored-procedure"
                })
                .ToList();

            var storedProceduresFolder = new TreeNode
            {
                Id = $"db-{projectId}-sps",
                Name = "Stored Procedures",
                Type = "stored-procedures-folder",
                Children = spChildren
            };

            // Build Functions folder (placeholder for future)
            var functionsFolder = new TreeNode
            {
                Id = $"db-{projectId}-funcs",
                Name = "Functions",
                Type = "functions-folder",
                Children = []
            };

            // Build Programmability folder
            var programmabilityFolder = new TreeNode
            {
                Id = $"db-{projectId}-prog",
                Name = "Programmability",
                Type = "programmability-folder",
                Children =
                [
                    storedProceduresFolder,
                    functionsFolder
                ]
            };

            // Build root database node
            var databaseNode = new TreeNode
            {
                Id = $"db-{projectId}",
                Name = databaseName,
                Type = "database",
                Children =
                [
                    tablesFolder,
                    programmabilityFolder
                ]
            };

            return databaseNode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building database tree for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(
        string connectionString,
        IEnumerable<string> tableNames)
    {
        try
        {
            return await _schemaRepository.GetForeignKeysAsync(connectionString, tableNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving foreign keys from database");
            throw;
        }
    }

    public async Task<IEnumerable<ForeignKeyScanResult>> GetForeignKeysAsync(
        string connectionString,
        IEnumerable<(string SchemaName, string TableName)> tables)
    {
        var qualifiedNames = tables
            .Select(t => $"{t.SchemaName}.{t.TableName}")
            .ToList();

        return await GetForeignKeysAsync(connectionString, qualifiedNames);
    }

    private static string NormalizeSqlOutsideLiterals(string definition)
    {
        var output = new StringBuilder(definition.Length);
        var outside = new StringBuilder(definition.Length);

        var i = 0;
        while (i < definition.Length)
        {
            var c = definition[i];

            if (c == '\'')
            {
                FlushOutsideSegment(outside, output);
                AppendSingleQuotedLiteral(definition, ref i, output);
                continue;
            }

            if (c == '"')
            {
                FlushOutsideSegment(outside, output);
                AppendDoubleQuotedLiteral(definition, ref i, output);
                continue;
            }

            if (c == '[')
            {
                FlushOutsideSegment(outside, output);
                AppendBracketedIdentifier(definition, ref i, output);
                continue;
            }

            outside.Append(c);
            i++;
        }

        FlushOutsideSegment(outside, output);
        return output.ToString().Trim();
    }

    private static void FlushOutsideSegment(StringBuilder outside, StringBuilder output)
    {
        if (outside.Length == 0)
        {
            return;
        }

        var noComments = StripSqlComments(outside.ToString());
        var collapsed = WhitespaceRegex().Replace(noComments, " ").ToLowerInvariant();
        output.Append(collapsed);
        outside.Clear();
    }

    private static string StripSqlComments(string input)
    {
        var result = new StringBuilder(input.Length);
        var i = 0;

        while (i < input.Length)
        {
            if (i + 1 < input.Length && input[i] == '/' && input[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/'))
                {
                    i++;
                }

                if (i + 1 < input.Length)
                {
                    i += 2;
                }

                continue;
            }

            if (i + 1 < input.Length && input[i] == '-' && input[i + 1] == '-')
            {
                i += 2;
                while (i < input.Length && input[i] != '\n' && input[i] != '\r')
                {
                    i++;
                }

                continue;
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static void AppendSingleQuotedLiteral(string text, ref int index, StringBuilder output)
    {
        output.Append(text[index++]);
        while (index < text.Length)
        {
            output.Append(text[index]);
            if (text[index] == '\'')
            {
                if (index + 1 < text.Length && text[index + 1] == '\'')
                {
                    output.Append(text[index + 1]);
                    index += 2;
                    continue;
                }

                index++;
                break;
            }

            index++;
        }
    }

    private static void AppendDoubleQuotedLiteral(string text, ref int index, StringBuilder output)
    {
        output.Append(text[index++]);
        while (index < text.Length)
        {
            output.Append(text[index]);
            if (text[index] == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    output.Append(text[index + 1]);
                    index += 2;
                    continue;
                }

                index++;
                break;
            }

            index++;
        }
    }

    private static void AppendBracketedIdentifier(string text, ref int index, StringBuilder output)
    {
        output.Append(text[index++]);
        while (index < text.Length)
        {
            output.Append(text[index]);
            if (text[index] == ']')
            {
                if (index + 1 < text.Length && text[index + 1] == ']')
                {
                    output.Append(text[index + 1]);
                    index += 2;
                    continue;
                }

                index++;
                break;
            }

            index++;
        }
    }

    public async Task<TableMetadataDto?> GetTableByIdAsync(int tableId)
    {
        try
        {
            return await _schemaRepository.GetTableByIdAsync(tableId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving table by ID {TableId}", tableId);
            throw;
        }
    }

    public async Task<ColumnMetadataDto?> GetColumnByIdAsync(int columnId)
    {
        try
        {
            return await _schemaRepository.GetColumnByIdAsync(columnId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving column by ID {ColumnId}", columnId);
            throw;
        }
    }

    public async Task<StoredProcedureMetadataDto?> GetSpByIdAsync(int spId)
    {
        try
        {
            return await _schemaRepository.GetSpByIdAsync(spId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored procedure by ID {SpId}", spId);
            throw;
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]+(\.[a-zA-Z0-9_]+)?$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
