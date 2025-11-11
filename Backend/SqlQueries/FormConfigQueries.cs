namespace ActoEngine.WebApi.SqlQueries;

public static class FormConfigSqlQueries
{
    public const string GetById = @"
        SELECT fc.ConfigJson
        FROM FormConfigs fc
        INNER JOIN Projects p ON fc.ProjectId = p.Id
        WHERE fc.Id = @Id AND p.CreatedBy = @UserId";

    public const string GetByIdOrName = @"
        SELECT fc.ConfigJson
        FROM FormConfigs fc
        INNER JOIN Projects p ON fc.ProjectId = p.Id
        WHERE (fc.Id = @IdOrName OR fc.FormName = @IdOrName)
          AND p.CreatedBy = @UserId";

    public const string GetIdByProjectAndFormName = @"
        SELECT fc.Id
        FROM FormConfigs fc
        INNER JOIN Projects p ON fc.ProjectId = p.Id
        WHERE fc.ProjectId = @ProjectId
          AND fc.FormName = @FormName
          AND p.CreatedBy = @UserId";

    public const string GetByProjectId = @"
        SELECT fc.Id, fc.ProjectId, fc.FormName, fc.TableName, fc.Title,
               fc.CreatedAt, fc.UpdatedAt
        FROM FormConfigs fc
        INNER JOIN Projects p ON fc.ProjectId = p.Id
        WHERE fc.ProjectId = @ProjectId AND p.CreatedBy = @UserId
        ORDER BY fc.UpdatedAt DESC";

    public const string GetByProjectIdWithoutUserFilter = @"
        SELECT Id, ProjectId, FormName, TableName, Title, CreatedAt, UpdatedAt
        FROM FormConfigs
        WHERE ProjectId = @ProjectId
        ORDER BY UpdatedAt DESC";

    public const string CheckExistsByProjectAndFormName = @"
        SELECT Id FROM FormConfigs
        WHERE ProjectId = @ProjectId AND FormName = @FormName";

    public const string CheckExistsById = @"
        SELECT Id FROM FormConfigs
        WHERE Id = @Id AND ProjectId = @ProjectId";

    public const string Update = @"
        UPDATE FormConfigs
        SET TableName = @TableName,
            Title = @Title,
            Description = @Description,
            ConfigJson = @ConfigJson,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @Id";

    public const string UpdateWithFormName = @"
        UPDATE FormConfigs
        SET TableName = @TableName,
            FormName = @FormName,
            Title = @Title,
            Description = @Description,
            ConfigJson = @ConfigJson,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @Id";

    public const string Insert = @"
        INSERT INTO FormConfigs (ProjectId, TableName, FormName, Title, Description, ConfigJson, CreatedAt, UpdatedAt)
        VALUES (@ProjectId, @TableName, @FormName, @Title, @Description, @ConfigJson, GETUTCDATE(), GETUTCDATE());
        SELECT SCOPE_IDENTITY();";

    public const string InsertWithCreatedBy = @"
        INSERT INTO FormConfigs (ProjectId, TableName, FormName, Title, Description, ConfigJson, CreatedBy, CreatedAt, UpdatedAt)
        VALUES (@ProjectId, @TableName, @FormName, @Title, @Description, @ConfigJson, @CreatedBy, GETUTCDATE(), GETUTCDATE())";

    public const string Delete = @"
        DELETE fc FROM FormConfigs fc
        INNER JOIN Projects p ON fc.ProjectId = p.Id
        WHERE fc.Id = @Id AND p.CreatedBy = @UserId";

    // Denormalized data queries
    public const string DeleteFormFields = @"
        DELETE FROM FormFields
        WHERE FormGroupId IN (
            SELECT Id FROM FormGroups
            WHERE FormConfigId = (SELECT Id FROM FormConfigs WHERE Id = @FormId)
        )";

    public const string DeleteFormGroups = @"
        DELETE FROM FormGroups
        WHERE FormConfigId = (SELECT Id FROM FormConfigs WHERE Id = @FormId)";

    public const string InsertFormGroup = @"
        INSERT INTO FormGroups (FormConfigId, GroupId, Title, Description, Layout, OrderIndex, Collapsible, Collapsed)
        OUTPUT INSERTED.Id
        VALUES ((SELECT Id FROM FormConfigs WHERE Id = @FormId), @GroupId, @Title, @Description, @Layout, @OrderIndex, @Collapsible, @Collapsed)";

    public const string InsertFormField = @"
        INSERT INTO FormFields (FormGroupId, FieldId, ColumnName, DataType, Label, InputType, Placeholder,
                                DefaultValue, HelpText, ColSize, OrderIndex, ValidationRules, IncludeInInsert,
                                IncludeInUpdate, IsPrimaryKey, IsIdentity, IsForeignKey, OptionsJson)
        VALUES (@FormGroupId, @FieldId, @ColumnName, @DataType, @Label, @InputType, @Placeholder,
                @DefaultValue, @HelpText, @ColSize, @OrderIndex, @ValidationRules, @IncludeInInsert,
                @IncludeInUpdate, @IsPrimaryKey, @IsIdentity, @IsForeignKey, @OptionsJson)";
}
