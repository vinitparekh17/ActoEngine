export type MappingStatus = "candidate" | "approved" | "ignored";
export type MappingType = "page_specific" | "shared";

export interface PageMapping {
  mappingId: number;
  projectId: number;
  domainName: string;
  pageName: string;
  storedProcedure: string;
  confidence?: number | null;
  source: string;
  status: MappingStatus;
  mappingType: MappingType;
  createdAt: string;
  updatedAt?: string;
  reviewedBy?: number | null;
  reviewedAt?: string | null;
}

export interface UpdateMappingRequest {
  status?: MappingStatus;
  mappingType?: MappingType;
  domainName?: string;
  pageName?: string;
  storedProcedure?: string;
}

export interface BulkMappingActionRequest {
  ids: number[];
  action: "approve" | "ignore";
}

export interface PageSpMappingPayload {
  pageName: string;
  domainName: string;
  serviceNames: string[];
  filtersRaw?: string | null;
  isNewPage?: boolean;
}

export interface PatchStatusRequestPayload {
  projectId: number;
  patchName?: string;
  pageMappings: PageSpMappingPayload[];
}

export interface PagePatchStatus {
  pageName: string;
  domainName: string;
  needsRegeneration: boolean;
  reason?: string;
  lastPatchDate?: string;
  fileLastModified?: string;
}

export interface PatchGenerationResponse {
  patchId: number;
  downloadPath: string;
  scriptDownloadPath?: string | null;
  filesIncluded: string[];
  warnings: string[];
  generatedAt: string;
}

export interface PatchHistoryRecord {
  patchId: number;
  projectId: number;
  pageName: string;
  domainName: string;
  spNames: string;
  isNewPage: boolean;
  patchName?: string;
  patchFilePath?: string;
  generatedAt: string;
  generatedBy?: number;
  status: string;
  pages: PatchPageEntry[];
}

export interface PatchPageEntry {
  domainName: string;
  pageName: string;
  isNewPage: boolean;
}

export interface ProjectPatchConfig {
  projectRootPath?: string;
  viewDirPath?: string;
  scriptDirPath?: string;
  patchDownloadPath?: string;
}
