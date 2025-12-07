import { useParams } from "react-router-dom";
import { ImpactAnalysis } from "@/components/impact-analysis/impact-analysis";

export default function ImpactAnalysisPage() {
  const { projectId, entityType, entityId } = useParams();

  if (!projectId || !entityType || !entityId) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center text-red-600">
          Invalid parameters: projectId, entityType, and entityId are required
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6">
      <ImpactAnalysis
        projectId={parseInt(projectId)}
        entityType={entityType}
        entityId={parseInt(entityId)}
        entityName={`${entityType} ${entityId}`}
      />
    </div>
  );
}
