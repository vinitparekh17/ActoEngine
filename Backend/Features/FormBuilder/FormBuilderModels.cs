using ActoEngine.WebApi.Features.Schema;

namespace ActoEngine.WebApi.Features.FormBuilder
{
    // ============================================
    // Request/Response Models
    // ============================================

    public class SaveFormConfigRequest
    {
        public int ProjectId { get; set; }
        public required FormConfig Config { get; set; }
        public string? Description { get; set; }
    }

    public class SaveFormConfigResponse
    {
        public bool Success { get; set; }
        public int FormId { get; set; }
        public required FormConfig Config { get; set; }
        public string? Message { get; set; }
    }

    public class LoadFormConfigRequest
    {
        public required string FormId { get; set; } // Can be numeric ID or form name
        public int UserId { get; set; }
    }

    public class GenerateFormRequest
    {
        public required FormConfig Config { get; set; }
        public bool GenerateStoredProcedures { get; set; } = false;
        public bool Preview { get; set; } = false;
    }

    public class GenerateFormResponse
    {
        public bool Success { get; set; }
        public required string Html { get; set; }
        public required string JavaScript { get; set; }
        public List<GeneratedSpInfo> StoredProcedures { get; set; } = [];
        public required string FileName { get; set; }
        public List<string>? Warnings { get; set; }
    }

    public class GeneratedSpInfo
    {
        public required string SpName { get; set; }
        public required string SpType { get; set; } // "CUD" or "SELECT"
        public required string Code { get; set; }
        public required string FileName { get; set; }
        public string? Description { get; set; }
    }

    // ============================================
    // Core Form Models
    // ============================================

    public class FormConfig
    {
        public string? Id { get; set; }
        public int ProjectId { get; set; }
        public required string TableName { get; set; }
        public required string FormName { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public List<FormGroup> Groups { get; set; } = [];
        public FormGenerationOptions? Options { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class FormGroup
    {
        public required string Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<FormField> Fields { get; set; } = [];
        public string Layout { get; set; } = "row"; // "row" or "column"
        public int Order { get; set; }
        public bool Collapsible { get; set; }
        public bool Collapsed { get; set; }
    }

    public class FormField
    {
        public required string Id { get; set; }
        public required string ColumnName { get; set; }
        public required string DataType { get; set; }
        public required string Label { get; set; }
        public required string InputType { get; set; }
        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public string? HelpText { get; set; }
        public int ColSize { get; set; } = 12;
        public int Order { get; set; }

        // Validation
        public bool Required { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public string? Pattern { get; set; }
        public string? ErrorMessage { get; set; }

        // Database flags
        public bool IncludeInInsert { get; set; } = true;
        public bool IncludeInUpdate { get; set; } = true;
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsForeignKey { get; set; }

        // Options for select/radio
        public ForeignKeyInfo? ForeignKeyInfo { get; set; }
        public List<SelectOption>? Options { get; set; }

        // UI state
        public bool Disabled { get; set; }
        public bool Readonly { get; set; }
    }

    public class SelectOption
    {
        public required string Label { get; set; }
        public required string Value { get; set; }
    }

    public class FormGenerationOptions
    {
        public string BootstrapVersion { get; set; } = "5";
        public string FormStyle { get; set; } = "vertical";
        public string LabelPosition { get; set; } = "top";
        public string JsFramework { get; set; } = "jquery";
        public string ValidationStyle { get; set; } = "inline";
        public bool GenerateGrid { get; set; } = true;
        public string GridType { get; set; } = "jqgrid";
        public string SpPrefix { get; set; } = "usp";
        public bool UseModal { get; set; } = true;
        public string ModalSize { get; set; } = "lg";
        public bool IncludePermissionChecks { get; set; } = true;
        public bool IncludeErrorHandling { get; set; } = true;
    }

    // ============================================
    // List/Summary Models
    // ============================================

    public class FormConfigListItem
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public required string TableName { get; set; }
        public required string FormName { get; set; }
        public required string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ============================================
    // Template Models
    // ============================================

    public class CodeTemplate
    {
        public int Id { get; set; }
        public required string TemplateName { get; set; }
        public required string TemplateType { get; set; } // "HTML", "JavaScript", "SP"
        public required string Framework { get; set; } // "Bootstrap5", "jQuery", etc.
        public required string Version { get; set; }
        public required string TemplateContent { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}