import { useLocation, useNavigate } from "react-router-dom";
import { useEffect } from "react";
import { toast } from "sonner";
import { ShieldAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

/**
 * Access Denied page shown when user tries to access a route without permission
 */
export function AccessDenied() {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as { requiredPermission?: string } | undefined;

  useEffect(() => {
    if (state?.requiredPermission) {
      toast.error(
        `Access Denied: Missing permission "${state.requiredPermission}"`,
      );
    }
  }, [state]);

  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="flex justify-center mb-4">
            <ShieldAlert className="h-16 w-16 text-destructive" />
          </div>
          <CardTitle className="text-2xl">Access Denied</CardTitle>
          <CardDescription>
            You don't have permission to access this page.
            {state?.requiredPermission && (
              <div className="mt-2 text-sm">
                Required permission:{" "}
                <code className="bg-muted px-2 py-1 rounded">
                  {state.requiredPermission}
                </code>
              </div>
            )}
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          <Button onClick={() => navigate("/dashboard")} className="w-full">
            Go to Dashboard
          </Button>
          <Button
            onClick={() => navigate(-1)}
            variant="outline"
            className="w-full"
          >
            Go Back
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
