import { useMemo } from "react";

export type DisplayMode = "table" | "input-table" | "cards" | "summary" | "ignore";

export interface PropMeta {
  id: string;
  label: string;
  active: boolean;
}

export interface Entity {
  id: string;
  name: string;
  path: string;
  parentPath: string;
  count: number;
  props: PropMeta[];
  displayMode: DisplayMode;
  isCollapsed: boolean;
}

export const buildPathGuard = (path: string): string => {
  const segments = path.split(".").map((s) => s.trim()).filter(Boolean);
  return segments.map((_, idx) => segments.slice(0, idx + 1).join(".")).join(" && ");
};

export function useTemplateGenerator(
  entities: Entity[],
  lowercaseKeys: boolean,
  tableClasses: string,
) {
  return useMemo(() => {
    if (!entities.length) return "";

    let template = `\n<div class="container-fluid">\n`;

    entities.forEach((entity) => {
      if (entity.displayMode === "ignore") return;
      const activeProps = entity.props.filter((p) => p.active);
      if (!activeProps.length) return;

      const pathCheck = lowercaseKeys && entity.path ? entity.path.toLowerCase() : entity.path;
      const safeCheck = pathCheck ? `{{if ${pathCheck.split(".")[0]}}}` : "";
      const safeCheckEnd = pathCheck ? "{{/if}}" : "";
      const fullPathGuard = pathCheck ? buildPathGuard(pathCheck) : "";

      if (safeCheck) template += `  ${safeCheck}\n`;

      if (entity.displayMode === "summary") {
        template += `  <div class="row mb-4">\n`;
        activeProps.forEach((p) => {
          const propName = lowercaseKeys ? p.id.toLowerCase() : p.id;
          template += `    <div class="col-12 col-md-6 col-lg-4 col-xl-3 mb-3">\n`;
          template += `      <div class="summary-item border rounded p-3 bg-light h-100">\n`;
          template += `        <strong class="d-block text-muted small mb-1">${p.label}</strong>\n`;
          template += `        <span>{{:${propName} || '-'}}</span>\n`;
          template += `      </div>\n`;
          template += `    </div>\n`;
        });
        template += "  </div>\n";
      }

      if (entity.displayMode === "table" || entity.displayMode === "input-table") {
        template += "  <div class=\"table-responsive mb-4\">\n";
        template += `    <table class="${tableClasses}" id="tbl${entity.name}">\n`;
        template += "      <thead class=\"table-light\">\n";
        template += "        <tr>\n";
        activeProps.forEach((p) => {
          template += `          <th>${p.label}</th>\n`;
        });
        template += "        </tr>\n";
        template += "      </thead>\n";
        template += "      <tbody>\n";
        if (pathCheck) {
          template += `        {{if ${fullPathGuard}}}{{for ${pathCheck}}}\n`;
        } else {
          template += "        {{for}}\n";
        }
        template += "        <tr>\n";
        activeProps.forEach((p) => {
          const propName = lowercaseKeys ? p.id.toLowerCase() : p.id;
          if (entity.displayMode === "input-table") {
            template += `          <td><input type="text" class="form-control form-control-sm" value="{{:${propName}}}" /></td>\n`;
          } else {
            template += `          <td>{{:${propName}}}</td>\n`;
          }
        });
        template += "        </tr>\n";
        template += pathCheck ? "        {{/for}}{{/if}}\n" : "        {{/for}}\n";
        template += "      </tbody>\n";
        template += "    </table>\n";
        template += "  </div>\n";
      }

      if (entity.displayMode === "cards") {
        template += "  <div class=\"row mb-4\">\n";
        template += pathCheck ? `    {{if ${fullPathGuard}}}{{for ${pathCheck}}}\n` : "    {{for}}\n";
        template += "    <div class=\"col-12 col-md-6 col-xl-4 mb-3\">\n";
        template += "    <div class=\"card h-100\">\n";
        template += "      <div class=\"card-body\">\n";
        activeProps.forEach((p) => {
          const propName = lowercaseKeys ? p.id.toLowerCase() : p.id;
          template += "      <div class=\"d-flex justify-content-between align-items-center border-bottom py-2\">\n";
          template += `        <span class="text-muted small">${p.label}</span>\n`;
          template += `        <span>{{:${propName}}}</span>\n`;
          template += "      </div>\n";
        });
        template += "      </div>\n";
        template += "    </div>\n";
        template += "    </div>\n";
        template += pathCheck ? "    {{/for}}{{/if}}\n" : "    {{/for}}\n";
        template += "  </div>\n";
      }

      if (safeCheckEnd) template += `  ${safeCheckEnd}\n\n`;
    });

    template += "</div>";
    return template;
  }, [entities, lowercaseKeys, tableClasses]);
}
