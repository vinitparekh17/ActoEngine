export function getDefaultSchema(dbType?: string): string {
    const normalized = (dbType || "").toLowerCase();

    // SQL Server variants
    if (normalized === "sqlserver" || normalized === "mssql" || normalized === "azure-sql") {
        return "dbo";
    }

    // PostgreSQL variants
    if (normalized === "postgres" || normalized === "postgresql") {
        return "public";
    }

    // For other databases (MySQL, Oracle, etc.), return empty string
    // as they may not have a default schema concept or it varies
    return "";
}