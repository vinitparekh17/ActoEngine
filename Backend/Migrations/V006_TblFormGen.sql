-- ============================================
-- Migration: V005_FormBuilder.sql
-- Description: Form Builder Database Schema
-- Date: 2025-11-22
-- ============================================

-- ============================================
-- 1. FormConfigs Table (Main Storage)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormConfigs' AND xtype='U')
BEGIN
    CREATE TABLE FormConfigs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId INT NOT NULL REFERENCES Projects(ProjectId),
        TableName NVARCHAR(100) NOT NULL,
        FormName NVARCHAR(100) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500),

        -- Serialized JSON config
        ConfigJson NVARCHAR(MAX) NOT NULL,

        -- Metadata
        CreatedBy INT REFERENCES Users(UserID),
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_FormConfigs_ProjectId ON FormConfigs(ProjectId);
    CREATE INDEX IX_FormConfigs_TableName ON FormConfigs(TableName);
END

-- ============================================
-- 2. FormGroups Table (Denormalized for querying)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormGroups' AND xtype='U')
BEGIN
    CREATE TABLE FormGroups (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormConfigId INT NOT NULL REFERENCES FormConfigs(Id),
        GroupId NVARCHAR(50) NOT NULL, -- Client-side temp ID
        Title NVARCHAR(200),
        Description NVARCHAR(500),
        Layout VARCHAR(20) DEFAULT 'row', -- 'row' | 'column'
        OrderIndex INT NOT NULL,
        Collapsible BIT DEFAULT 0,
        Collapsed BIT DEFAULT 0
    );

    CREATE INDEX IX_FormGroups_FormConfigId ON FormGroups(FormConfigId);
END

-- ============================================
-- 3. FormFields Table (Denormalized for querying)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormFields' AND xtype='U')
BEGIN
    CREATE TABLE FormFields (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormGroupId INT NOT NULL REFERENCES FormGroups(Id),

        -- Database mapping
        ColumnName NVARCHAR(100) NOT NULL,
        DataType NVARCHAR(50) NOT NULL,

        -- UI Configuration
        Label NVARCHAR(200) NOT NULL,
        InputType VARCHAR(50) NOT NULL,
        Placeholder NVARCHAR(200),
        DefaultValue NVARCHAR(200),
        HelpText NVARCHAR(500),

        -- Layout
        ColSize INT DEFAULT 12,
        OrderIndex INT NOT NULL,

        -- Validation (JSON for complex rules)
        ValidationRules NVARCHAR(MAX),

        -- Database operations
        IncludeInInsert BIT DEFAULT 1,
        IncludeInUpdate BIT DEFAULT 1,
        IsPrimaryKey BIT DEFAULT 0,
        IsIdentity BIT DEFAULT 0,
        IsForeignKey BIT DEFAULT 0,

        -- Select/Radio options (JSON)
        OptionsJson NVARCHAR(MAX)
    );

    CREATE INDEX IX_FormFields_FormGroupId ON FormFields(FormGroupId);
END

-- ============================================
-- 4. GenerationHistory Table (Audit Trail)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GenerationHistory' AND xtype='U')
BEGIN
    CREATE TABLE GenerationHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormConfigId INT NOT NULL REFERENCES FormConfigs(Id),

        -- What was generated
        GenerationType VARCHAR(20) NOT NULL, -- 'Full' | 'Preview'
        HtmlGenerated BIT DEFAULT 0,
        JavaScriptGenerated BIT DEFAULT 0,
        SpGenerated BIT DEFAULT 0,

        -- Generation details
        FieldCount INT NOT NULL,
        GroupCount INT NOT NULL,
        Warnings NVARCHAR(MAX), -- JSON array

        -- Audit
        GeneratedBy INT REFERENCES Users(UserID),
        GeneratedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_GenerationHistory_FormConfigId ON GenerationHistory(FormConfigId);
    CREATE INDEX IX_GenerationHistory_GeneratedAt ON GenerationHistory(GeneratedAt DESC);
END

-- ============================================
-- 5. CodeTemplates Table (Template Management)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CodeTemplates' AND xtype='U')
BEGIN
    CREATE TABLE CodeTemplates (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TemplateName NVARCHAR(100) NOT NULL UNIQUE,
        TemplateType VARCHAR(50) NOT NULL, -- 'HTML' | 'JavaScript' | 'SP'
        Framework VARCHAR(50) NOT NULL, -- 'Bootstrap5' | 'jQuery'
        Version VARCHAR(20) NOT NULL,

        -- Template content (Scriban/Handlebars syntax)
        TemplateContent NVARCHAR(MAX) NOT NULL,

        -- Metadata
        Description NVARCHAR(500),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_CodeTemplates_Type_Framework ON CodeTemplates(TemplateType, Framework);
END
-- ============================================
-- 1. FormConfigs Table (Main Storage)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormConfigs' AND xtype='U')
BEGIN
    CREATE TABLE FormConfigs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId INT NOT NULL REFERENCES Projects(ProjectId),
        TableName NVARCHAR(100) NOT NULL,
        FormName NVARCHAR(100) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500),

        -- Serialized JSON config
        ConfigJson NVARCHAR(MAX) NOT NULL,

        -- Metadata
    CreatedBy INT REFERENCES Users(UserID),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),        -- Indexes
        CONSTRAINT FK_FormConfigs_Project FOREIGN KEY (ProjectId)
            REFERENCES Projects(Id) ON DELETE CASCADE,
        INDEX IX_FormConfigs_ProjectId (ProjectId),
        INDEX IX_FormConfigs_TableName (TableName)
    );
END

-- ============================================
-- 2. FormGroups Table (Denormalized for querying)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormGroups' AND xtype='U')
BEGIN
    CREATE TABLE FormGroups (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormConfigId INT NOT NULL,
        GroupId NVARCHAR(50) NOT NULL, -- Client-side temp ID
        Title NVARCHAR(200),
        Description NVARCHAR(500),
        Layout VARCHAR(20) DEFAULT 'row', -- 'row' | 'column'
        OrderIndex INT NOT NULL,
        Collapsible BIT DEFAULT 0,
        Collapsed BIT DEFAULT 0,

        CONSTRAINT FK_FormGroups_FormConfig FOREIGN KEY (FormConfigId)
            REFERENCES FormConfigs(Id) ON DELETE CASCADE,
        INDEX IX_FormGroups_FormConfigId (FormConfigId)
    );
END

-- ============================================
-- 3. FormFields Table (Denormalized for querying)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FormFields' AND xtype='U')
BEGIN
    CREATE TABLE FormFields (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormGroupId INT NOT NULL,
        FieldId NVARCHAR(50) NOT NULL, -- Client-side temp ID

        -- Database mapping
        ColumnName NVARCHAR(100) NOT NULL,
        DataType NVARCHAR(50) NOT NULL,

        -- UI Configuration
        Label NVARCHAR(200) NOT NULL,
        InputType VARCHAR(50) NOT NULL,
        Placeholder NVARCHAR(200),
        DefaultValue NVARCHAR(200),
        HelpText NVARCHAR(500),

        -- Layout
        ColSize INT DEFAULT 12,
        OrderIndex INT NOT NULL,

        -- Validation (JSON for complex rules)
        ValidationRules NVARCHAR(MAX),

        -- Database operations
        IncludeInInsert BIT DEFAULT 1,
        IncludeInUpdate BIT DEFAULT 1,
        IsPrimaryKey BIT DEFAULT 0,
        IsIdentity BIT DEFAULT 0,
        IsForeignKey BIT DEFAULT 0,

        -- Select/Radio options (JSON)
        OptionsJson NVARCHAR(MAX),

        CONSTRAINT FK_FormFields_FormGroup FOREIGN KEY (FormGroupId)
            REFERENCES FormGroups(Id) ON DELETE CASCADE,
        INDEX IX_FormFields_FormGroupId (FormGroupId)
    );
END

-- ============================================
-- 4. GenerationHistory Table (Audit Trail)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GenerationHistory' AND xtype='U')
BEGIN
    CREATE TABLE GenerationHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FormConfigId INT NOT NULL,

        -- What was generated
        GenerationType VARCHAR(20) NOT NULL, -- 'Full' | 'Preview'
        HtmlGenerated BIT DEFAULT 0,
        JavaScriptGenerated BIT DEFAULT 0,
        SpGenerated BIT DEFAULT 0,

        -- Generation details
        FieldCount INT NOT NULL,
        GroupCount INT NOT NULL,
        Warnings NVARCHAR(MAX), -- JSON array

        -- Audit
        GeneratedBy INT,
        GeneratedAt DATETIME2 DEFAULT GETUTCDATE(),

        CONSTRAINT FK_GenerationHistory_FormConfig FOREIGN KEY (FormConfigId)
            REFERENCES FormConfigs(Id) ON DELETE CASCADE,
        INDEX IX_GenerationHistory_FormConfigId (FormConfigId),
        INDEX IX_GenerationHistory_GeneratedAt (GeneratedAt DESC)
    );
END

-- ============================================
-- 5. CodeTemplates Table (Template Management)
-- ============================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CodeTemplates' AND xtype='U')
BEGIN
    CREATE TABLE CodeTemplates (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TemplateName NVARCHAR(100) NOT NULL UNIQUE,
        TemplateType VARCHAR(50) NOT NULL, -- 'HTML' | 'JavaScript' | 'SP'
        Framework VARCHAR(50) NOT NULL, -- 'Bootstrap5' | 'jQuery'
        Version VARCHAR(20) NOT NULL,

        -- Template content (Scriban/Handlebars syntax)
        TemplateContent NVARCHAR(MAX) NOT NULL,

        -- Metadata
        Description NVARCHAR(500),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),

        INDEX IX_CodeTemplates_Type_Framework (TemplateType, Framework)
    );
END

-- ============================================
-- Insert Default Templates
-- ============================================
IF NOT EXISTS (SELECT 1 FROM CodeTemplates WHERE TemplateName = 'Bootstrap5_HTML')
BEGIN
    INSERT INTO CodeTemplates (TemplateName, TemplateType, Framework, Version, Description, TemplateContent, IsActive)
    VALUES (
        'Bootstrap5_HTML',
        'HTML',
        'Bootstrap5',
        '1.0.0',
        'Bootstrap 5 HTML Form Template',
        '<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{{ form.title }}</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css">
</head>
<body>
    <div class="container-fluid py-4">
        <div class="row justify-content-center">
            <div class="col-lg-8">
                <div class="card shadow">
                    <div class="card-header bg-primary text-white">
                        <h4 class="mb-0"><i class="fas fa-edit me-2"></i>{{ form.title }}</h4>
                        {{- if form.description }}
                        <p class="mb-0 mt-2">{{ form.description }}</p>
                        {{- end }}
                    </div>
                    <div class="card-body">
                        <form id="frm{{ form.form_name }}" class="needs-validation" novalidate>
                            {{- for group in form.groups }}
                            <div class="row mb-4">
                                {{- if group.title }}
                                <div class="col-12">
                                    <h5 class="mb-3 text-primary">
                                        {{- if group.collapsible }}
                                        <button class="btn btn-link p-0 text-decoration-none" type="button" data-bs-toggle="collapse" data-bs-target="#group{{ group.id }}">
                                            <i class="fas fa-chevron-down me-2"></i>{{ group.title }}
                                        </button>
                                        {{- else }}
                                        {{ group.title }}
                                        {{- end }}
                                    </h5>
                                    {{- if group.description }}
                                    <p class="text-muted small mb-3">{{ group.description }}</p>
                                    {{- end }}
                                </div>
                                {{- end }}

                                <div class="col-12 {{- if group.collapsible }} collapse show {{- end }}" id="group{{ group.id }}">
                                    <div class="row g-3">
                                        {{- for field in group.fields }}
                                        <div class="col-md-{{ field.col_size }} mb-3">
                                            <label for="{{ field.column_name }}" class="form-label">
                                                {{ field.label }}
                                                {{- if field.required }}<span class="text-danger">*</span>{{- end }}
                                            </label>

                                            {{- if field.input_type == "select" }}
                                            <select id="{{ field.column_name }}" name="{{ field.column_name }}"
                                                    class="form-select" {{- if field.required }} required{{- end }}>
                                                <option value="">Choose...</option>
                                                {{- if field.options && field.options.size > 0 }}
                                                    {{- for option in field.options }}
                                                    <option value="{{ option.value }}">{{ option.label }}</option>
                                                    {{- end }}
                                                {{- else if field.foreign_key_info }}
                                                    <!-- TODO: Populate dynamically from {{ field.foreign_key_info.referenced_table }}
                                                         using {{ field.foreign_key_info.display_column }} as label
                                                         and {{ field.foreign_key_info.referenced_column }} as value -->
                                                {{- end }}
                                            </select>

                                            {{- else if field.input_type == "textarea" }}
                                            <textarea id="{{ field.column_name }}" name="{{ field.column_name }}"
                                                      class="form-control" placeholder="{{ field.placeholder }}"
                                                      rows="3" {{- if field.required }} required{{- end }}></textarea>
                                            {{- else if field.input_type == "checkbox" }}
                                            <div class="form-check">
                                                <input class="form-check-input" type="checkbox"
                                                       id="{{ field.column_name }}" name="{{ field.column_name }}"
                                                       value="true" {{- if field.required }} required{{- end }}>
                                                <label class="form-check-label" for="{{ field.column_name }}">
                                                    {{ field.label }}
                                                </label>
                                            </div>
                                            {{- else if field.input_type == "radio" }}
                                            <div>
                                                {{- for option in field.options }}
                                                <div class="form-check">
                                                    <input class="form-check-input" type="radio"
                                                           id="{{ field.column_name }}_{{ option.value }}"
                                                           name="{{ field.column_name }}" value="{{ option.value }}"
                                                           {{- if field.required }} required{{- end }}>
                                                    <label class="form-check-label" for="{{ field.column_name }}_{{ option.value }}">
                                                        {{ option.label }}
                                                    </label>
                                                </div>
                                                {{- end }}
                                            </div>
                                            {{- else }}
                                            <input type="{{ field.input_type }}" id="{{ field.column_name }}"
                                                   name="{{ field.column_name }}" class="form-control"
                                                   placeholder="{{ field.placeholder }}"
                                                   {{- if field.default_value }} value="{{ field.default_value }}"{{- end }}
                                                   {{- if field.required }} required{{- end }}
                                                   {{- if field.min_length }} minlength="{{ field.min_length }}"{{- end }}
                                                   {{- if field.max_length }} maxlength="{{ field.max_length }}"{{- end }}>
                                            {{- end }}

                                            {{- if field.help_text }}
                                            <div class="form-text">{{ field.help_text }}</div>
                                            {{- end }}
                                            <div class="invalid-feedback">Please provide a valid {{ field.label }}.</div>
                                        </div>
                                        {{- end }}
                                    </div>
                                </div>
                            </div>
                            {{- end }}

                            <div class="row mt-4">
                                <div class="col-12">
                                    <button type="submit" id="btnSave{{ form.form_name }}" class="btn btn-primary me-2">
                                        <i class="fas fa-save me-1"></i>Save
                                    </button>
                                    <button type="button" id="btnCancel{{ form.form_name }}" class="btn btn-secondary">
                                        <i class="fas fa-times me-1"></i>Cancel
                                    </button>
                                </div>
                            </div>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"></script>
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="{{ form.form_name }}.js"></script>
</body>
</html>',
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM CodeTemplates WHERE TemplateName = 'jQuery_JS')
BEGIN
    INSERT INTO CodeTemplates (TemplateName, TemplateType, Framework, Version, Description, TemplateContent, IsActive)
    VALUES (
        'jQuery_JS',
        'JavaScript',
        'jQuery',
        '1.0.0',
        'jQuery JavaScript Form Handler Template',
        'var {{ form.form_name }}View = {
    variables: {
        BindUrl: "/Common/BindDetails?ServiceName={{ form.table_name }}_SELECT",
        OperationUrl: "/Common/Operations?ServiceName={{ form.table_name }}_CUD",
        Oper: ''Add'',
        File: "{{ form.form_name }}.js",
        MasterId: "",
    },

    // Initialize form
    initializeForm: function() {
        this.bindEvents();
        this.clearControls();
    },

    // Bind form events
    bindEvents: function() {
        var self = this;

        // Save button
        $("#btnSave{{ form.form_name }}").click(function(e) {
            e.preventDefault();
            self.saveData();
        });

        // Cancel button
        $("#btnCancel{{ form.form_name }}").click(function(e) {
            e.preventDefault();
            self.clearControls();
        });

        // Form validation
        $("#frm{{ form.form_name }}").submit(function(e) {
            if (!this.checkValidity()) {
                e.preventDefault();
                e.stopPropagation();
            }
            $(this).addClass(''was-validated'');
        });
    },

    // Save form data
    saveData: function() {
        if (!this.validateForm()) return;

        var formData = this.getFormData();
        var oper = this.variables.Oper;

        $.ajax({
            url: this.variables.OperationUrl,
            type: "POST",
            data: {
                oper: oper,
                data: JSON.stringify(formData)
            },
            success: function(response) {
                if (response.success) {
                    alert(oper === "Add" ? "Record saved successfully!" : "Record updated successfully!");
                    {{ form.form_name }}View.clearControls();
                    {{- if form.options.generate_grid }}
                    {{ form.form_name }}View.refreshGrid();
                    {{- end }}
                } else {
                    alert("Error: " + response.message);
                }
            },
            error: function(xhr, status, error) {
                alert("Error saving data: " + error);
            }
        });
    },

    // Validate form
    validateForm: function() {
        var isValid = true;
        var errors = [];

        {{- for group in form.groups }}
        {{- for field in group.fields }}
        {{- if field.required }}
        if (!$("#{{ field.column_name }}").val()) {
            errors.push("{{ field.label }} is required");
            isValid = false;
        }
        {{- end }}
        {{- if field.min_length }}
        if ($("#{{ field.column_name }}").val() && $("#{{ field.column_name }}").val().length < {{ field.min_length }}) {
            errors.push("{{ field.label }} must be at least {{ field.min_length }} characters");
            isValid = false;
        }
        {{- end }}
        {{- if field.max_length }}
        if ($("#{{ field.column_name }}").val() && $("#{{ field.column_name }}").val().length > {{ field.max_length }}) {
            errors.push("{{ field.label }} cannot exceed {{ field.max_length }} characters");
            isValid = false;
        }
        {{- end }}
        {{- end }}
        {{- end }}

        if (!isValid) {
            alert("Please correct the following errors:\n" + errors.join("\n"));
        }

        return isValid;
    },

    // Get form data
    getFormData: function() {
        return {
            {{- for group in form.groups }}
            {{- for field in group.fields }}
            {{ field.column_name }}: $("#{{ field.column_name }}").val(),
            {{- end }}
            {{- end }}
        };
    },

    // Clear form controls
    clearControls: function() {
        $("#frm{{ form.form_name }}")[0].reset();
        $("#frm{{ form.form_name }}").removeClass(''was-validated'');
        this.variables.Oper = "Add";
        this.variables.MasterId = "";
    },

    // Load record for editing
    loadRecord: function(id) {
        var self = this;
        this.variables.MasterId = id;
        this.variables.Oper = "Edit";

        $.ajax({
            url: this.variables.BindUrl,
            type: "GET",
            data: { id: id },
            success: function(response) {
                if (response.success && response.data) {
                    self.populateForm(response.data);
                }
            }
        });
    },

    // Populate form with data
    populateForm: function(data) {
        {{- for group in form.groups }}
        {{- for field in group.fields }}
        $("#{{ field.column_name }}").val(data.{{ field.column_name }});
        {{- end }}
        {{- end }}
    }{{- if form.options.generate_grid }},

    // Initialize jqGrid
    initializeGrid: function() {
        $("#grid{{ form.form_name }}").jqGrid({
            url: this.variables.BindUrl,
            datatype: "json",
            colModel: [
                {{- for group in form.groups }}
                {{- for field in group.fields }}
                { label: "{{ field.label }}", name: "{{ field.column_name }}", width: 150 },
                {{- end }}
                {{- end }}
                { label: "Actions", name: "actions", width: 100, formatter: this.actionFormatter }
            ],
            viewrecords: true,
            height: 400,
            rowNum: 10,
            pager: "#pager{{ form.form_name }}"
        });
    },

    // Action formatter for grid
    actionFormatter: function(cellvalue, options, rowObject) {
        return ''<button class="btn btn-sm btn-primary" onclick="{{ form.form_name }}View.loadRecord('' + rowObject.Id + '')">Edit</button>'';
    },

    // Refresh grid
    refreshGrid: function() {
        $("#grid{{ form.form_name }}").trigger("reloadGrid");
    }{{- end }}
};

// Initialize on document ready
$(document).ready(function() {
    {{ form.form_name }}View.initializeForm();
    {{- if form.options.generate_grid }}
    {{ form.form_name }}View.initializeGrid();
    {{- end }}
});',
        1
    );
END