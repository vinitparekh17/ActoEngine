export interface EntityRef {
  type: number;
  id: number;
  name: string | null;
  stableKey: string;
}

export interface ImpactReason {
  priority: number;
  statement: string;
  implication: string;
  evidence: string[];
}

export interface ImpactVerdict {
  risk: number;
  requiresApproval: boolean;
  summary: string;
  reasons: ImpactReason[];
  generatedAt: string;
}

export interface ImpactSummary {
  triggeringEntity: EntityRef;
  rootEntity?: EntityRef;
  environment: string;
  analysisType: string;
  action: string;
}

export interface ImpactPathNode {
  type: number;
  name: string | null;
  stableKey: string;
  iconName?: string | null;
}

export interface ImpactPath {
  nodes: ImpactPathNode[];
  edges?: string[]; // DependencyType values from backend (e.g., "Select", "Update")
}

export interface ImpactEntity {
  entity: EntityRef;
  worstCaseImpactLevel: number;
  riskScore: number;
  dominantOperation: string;
  paths: ImpactPath[];
}

export interface ImpactDecisionResponse {
  verdict: ImpactVerdict;
  summary: ImpactSummary;
  entities: ImpactEntity[];
}

// --- LEGACY TYPES ---
// These are used by existing components like src/components/impact-analysis/impact-analysis.tsx

export type ImpactLevel = "CRITICAL" | "HIGH" | "MEDIUM" | "LOW";

export interface AffectedEntity {
  entityType: string;
  entityId: number;
  entityName: string;
  owner: string;
  depth: number;
  impactLevel: ImpactLevel;
  riskScore: number;
  reason: string;
  criticalityLevel: number;
}

export interface GraphEdge {
  source: string;
  target: string;
  type: string;
}

export interface ImpactLegacySummary {
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
}

export interface ImpactAnalysisResponse {
  rootId: string;
  totalRiskScore: number;
  requiresApproval: boolean;
  summary: ImpactLegacySummary;
  affectedEntities: AffectedEntity[];
  graphEdges: GraphEdge[];
}
