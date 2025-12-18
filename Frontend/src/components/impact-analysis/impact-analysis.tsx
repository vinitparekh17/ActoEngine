import React, { useState } from "react";
import { useApi } from "@/hooks/useApi";
import { TableSkeleton, CardSkeleton } from "@/components/ui/skeletons";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

import { AlertTriangle, AlertCircle, Info, CheckCircle } from "lucide-react";
import type {
  ImpactAnalysisResponse,
  AffectedEntity,
  ImpactLevel,
} from "@/types/impact-analysis";

interface ImpactAnalysisProps {
  projectId: number;
  entityType: string;
  entityId: number;
  entityName: string;
}

export const ImpactAnalysis: React.FC<ImpactAnalysisProps> = ({
  projectId,
  entityType,
  entityId,
  entityName,
}) => {
  const [changeType, setChangeType] = useState<"MODIFY" | "DELETE">("MODIFY");

  const {
    data: impact,
    isLoading: loading,
    error,
  } = useApi<ImpactAnalysisResponse>(
    `/projects/${projectId}/impact/${encodeURIComponent(entityType)}/${encodeURIComponent(String(entityId))}?changeType=${encodeURIComponent(changeType)}`,
  );

  const getImpactIcon = (level: string) => {
    const classes: Record<string, string> = {
      CRITICAL: "text-red-600",
      HIGH: "text-yellow-600",
      MEDIUM: "text-blue-600",
      LOW: "text-green-600",
    };
    const IconMap: Record<string, any> = {
      CRITICAL: AlertTriangle,
      HIGH: AlertCircle,
      MEDIUM: Info,
      LOW: CheckCircle,
    };
    const Icon = IconMap[level];
    return Icon ? <Icon className={classes[level]} size={20} /> : null;
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <CardSkeleton />
        <TableSkeleton rows={5} columns={6} />
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Error</AlertTitle>
        <AlertDescription>
          {error instanceof Error
            ? error.message
            : "Failed to load impact analysis"}
        </AlertDescription>
      </Alert>
    );
  }

  if (!impact) return null;

  return (
    <div className="space-y-4">
      {/* Change Type Selector */}
      <Card>
        <CardHeader>
          <CardTitle>Impact Analysis for {entityName}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex gap-2">
            <Button
              variant={changeType === "MODIFY" ? "default" : "outline"}
              onClick={() => setChangeType("MODIFY")}
            >
              Modify
            </Button>

            <Button
              variant={changeType === "DELETE" ? "destructive" : "outline"}
              onClick={() => setChangeType("DELETE")}
            >
              Delete
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Risk Summary */}
      {impact.totalRiskScore > 0 && (
        <Alert
          className={
            impact.totalRiskScore > 70
              ? "border-red-600 text-red-600"
              : impact.totalRiskScore > 40
                ? "border-yellow-500 text-yellow-600"
                : "border-blue-500 text-blue-600"
          }
        >
          <AlertTitle className="flex items-center gap-2">
            Risk Score: {impact.totalRiskScore}/100
            {impact.requiresApproval && (
              <Badge className="bg-red-600 text-white">Requires Approval</Badge>
            )}
          </AlertTitle>

          <AlertDescription>
            <div className="flex gap-4 mt-3 text-sm">
              <span>
                <Badge className="bg-red-600 text-white">
                  {impact.summary.criticalCount}
                </Badge>{" "}
                Critical
              </span>
              <span>
                <Badge className="bg-yellow-500 text-white">
                  {impact.summary.highCount}
                </Badge>{" "}
                High
              </span>
              <span>
                <Badge className="bg-blue-500 text-white">
                  {impact.summary.mediumCount}
                </Badge>{" "}
                Medium
              </span>
              <span>
                <Badge className="bg-green-600 text-white">
                  {impact.summary.lowCount}
                </Badge>{" "}
                Low
              </span>
            </div>
          </AlertDescription>
        </Alert>
      )}

      {/* Affected Entities Table */}
      <Card>
        <CardHeader>
          <CardTitle>
            Affected Entities ({impact.affectedEntities.length})
          </CardTitle>
        </CardHeader>

        <CardContent>
          {impact.affectedEntities.length === 0 ? (
            <Alert className="border-green-600 text-green-700">
              <AlertTitle>Safe</AlertTitle>
              <AlertDescription>
                No dependencies found. This change appears safe.
              </AlertDescription>
            </Alert>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Impact</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead>Criticality</TableHead>
                </TableRow>
              </TableHeader>

              <TableBody>
                {impact.affectedEntities.map((entity) => (
                  <TableRow key={`${entity.entityType}-${entity.entityId}`}>
                    <TableCell>{getImpactIcon(entity.impactLevel)}</TableCell>

                    <TableCell>
                      <Badge variant="secondary">{entity.entityType}</Badge>
                    </TableCell>

                    <TableCell className="font-medium">
                      {entity.entityName}
                    </TableCell>

                    <TableCell>{entity.owner || "-"}</TableCell>

                    <TableCell className="text-muted-foreground text-sm">
                      {entity.reason}
                    </TableCell>

                    <TableCell>
                      <Badge variant="outline">
                        {entity.criticalityLevel}/5
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
};
