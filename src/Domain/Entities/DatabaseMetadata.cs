namespace ActoX.Domain.Entities;

public class DBTableMeta
{
    public required string DatabaseId { get; set; }
    public required string TableName { get; set; }
    public required List<string> Columns { get; set; }
    public required List<string> Relationships { get; set; }
    public required int MyProperty { get; set; }
}