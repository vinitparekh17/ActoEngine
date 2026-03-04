import type { SPConfigValues } from "@/schema/spBuilderSchema";
import type { SPType } from "./SPTypeCard";

export const CUD_SP_CONFIG_DEFAULTS: Extract<SPConfigValues, { mode: "CUD" }> = {
  mode: "CUD",
  generateCreate: true,
  generateUpdate: true,
  generateDelete: true,
  spPrefix: "usp",
  includeErrorHandling: true,
  includeTransaction: true,
  actionParamName: "Action",
  includeInCreate: {},
  includeInUpdate: {},
};

export const SELECT_SP_CONFIG_DEFAULTS: Extract<SPConfigValues, { mode: "SELECT" }> = {
  mode: "SELECT",
  includePagination: true,
  orderBy: [],
  filters: [],
};

export const getDefaultSpConfig = (type: SPType): SPConfigValues =>
  type === "CUD"
    ? {
      ...CUD_SP_CONFIG_DEFAULTS,
      includeInCreate: {},
      includeInUpdate: {},
    }
    : {
      ...SELECT_SP_CONFIG_DEFAULTS,
      orderBy: [],
      filters: [],
    };
