import { z } from "zod";

export type TreeNode = {
    id: string;
    name: string;
    children?: TreeNode[];
    type?:
    | "database" | "table" | "column" | "index"
    | "stored-procedure" | "scalar-function" | "table-function"
    | "tables-folder" | "programmability-folder"
    | "stored-procedures-folder" | "functions-folder";
};

export const CUDSchema = z.object({
    mode: z.literal("CUD"),
    generateCreate: z.boolean().default(true),
    generateUpdate: z.boolean().default(true),
    generateDelete: z.boolean().default(true),
    spPrefix: z.string().min(1).default("usp"),
    includeErrorHandling: z.boolean().default(true),
    includeTransaction: z.boolean().default(true),
    actionParamName: z.string().min(1).default("Action"),
    includeInCreate: z.record(z.string(), z.boolean()).default({}),
    includeInUpdate: z.record(z.string(), z.boolean()).default({}),
});

export const FilterSchema = z.object({
    column: z.string().min(1, "Column required"),
    operator: z.enum(["=", "LIKE", ">", "<", "BETWEEN"]),
    optional: z.boolean().default(false),
});

export const SELECTSchema = z.object({
    mode: z.literal("SELECT"),
    includePagination: z.boolean().default(true),
    orderBy: z.array(z.string()).default([]),
    filters: z.array(FilterSchema).default([]),
});

export const ConfigSchema = z.discriminatedUnion("mode", [CUDSchema, SELECTSchema]);

export type SPConfigValues = z.infer<typeof ConfigSchema>;
