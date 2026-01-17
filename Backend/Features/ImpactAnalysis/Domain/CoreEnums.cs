namespace ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

public enum EntityType
{
    Table,
    View,
    Sp,
    Function,
    Api,           // future
    Job            // future
}

public enum ChangeType
{
    Create,
    Modify,
    Delete
}

public enum DependencyType
{
    Unknown = 0,

    Select,
    Insert,
    Update,
    Delete,

    SchemaDependency,
    ApiCall
}

public enum ImpactLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
