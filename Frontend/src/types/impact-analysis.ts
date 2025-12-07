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

export interface ImpactSummary {
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
}

export interface ImpactAnalysisResponse {
  rootId: string;
  totalRiskScore: number;
  requiresApproval: boolean;
  summary: ImpactSummary;
  affectedEntities: AffectedEntity[];
  graphEdges: GraphEdge[];
}
