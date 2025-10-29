namespace ActoEngine.WebApi.Services.SpBuilder;

public static class SpTemplateStore
{
    // CUD Template - Create, Update, Delete in ONE SP
    public const string CudTemplate = @"CREATE PROCEDURE [dbo].[{SP_NAME}]
    @{ACTION_PARAM} CHAR(1), -- 'C' = Create, 'U' = Update, 'D' = Delete
{PARAMETERS}
AS
BEGIN
{ERROR_HANDLING_START}
    -- CREATE
    IF @{ACTION_PARAM} = 'C'
    BEGIN
        INSERT INTO [dbo].[{TABLE_NAME}] (
{INSERT_COLUMNS}
        )
        VALUES (
{INSERT_VALUES}
        );
{RETURN_IDENTITY}
    END
    
    -- UPDATE
    ELSE IF @{ACTION_PARAM} = 'U'
    BEGIN
        UPDATE [dbo].[{TABLE_NAME}]
        SET
{UPDATE_SET_CLAUSE}
        WHERE
{WHERE_CLAUSE};
    END
    
    -- DELETE
    ELSE IF @{ACTION_PARAM} = 'D'
    BEGIN
        DELETE FROM [dbo].[{TABLE_NAME}]
        WHERE
{WHERE_CLAUSE};
    END
{ERROR_HANDLING_END}
END";

    // SELECT Template - with filters and optional pagination
    public const string SelectTemplate = @"CREATE PROCEDURE [dbo].[{SP_NAME}]
{FILTER_PARAMETERS}{PAGINATION_PARAMS}
AS
BEGIN
    SET NOCOUNT ON;
{PAGINATION_LOGIC}
    SELECT
{SELECT_COLUMNS}
    FROM [dbo].[{TABLE_NAME}]
{WHERE_FILTERS}
    ORDER BY
{ORDER_BY_CLAUSE}{PAGINATION_FETCH};
{TOTAL_COUNT}
END";

    // Error handling blocks
    public const string ErrorHandlingStart = @"    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
";

    public const string ErrorHandlingStartNoTrans = @"    SET NOCOUNT ON;
    BEGIN TRY
";

    public const string ErrorHandlingEnd = @"        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH";

    public const string ErrorHandlingEndNoTrans = @"    END TRY
    BEGIN CATCH
        THROW;
    END CATCH";
}