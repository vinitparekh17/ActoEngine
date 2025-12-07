// pages/context/ContextExperts.tsx
import React, { useState, useMemo } from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Search,
  Crown,
  Star,
  User as UserIcon,
  GitCommit,
  Mail,
  Database,
  Code2,
  AlertCircle,
  Loader2,
  ExternalLink,
  FileText,
  Filter,
  Users,
} from "lucide-react";
import { Link } from "react-router-dom";
import { TableMetadataDto, StoredProcedureMetadataDto } from "@/types/context";

// Types
interface Expert {
  userId: number;
  user: {
    fullName?: string;
    username: string;
    email: string;
  };
  expertiseLevel: "OWNER" | "EXPERT" | "FAMILIAR" | "CONTRIBUTOR";
  notes?: string;
  assignedAt: string;
}

interface EntityWithExperts {
  entityType: "TABLE" | "SP";
  entityId: number;
  entityName: string;
  schemaName?: string;
  experts: Expert[];
  completenessScore?: number;
  isStale?: boolean;
}

interface ExpertSummary {
  userId: number;
  user: {
    fullName?: string;
    username: string;
    email: string;
  };
  entityCount: number;
  expertiseBreakdown: Record<string, number>;
  lastActivity?: string;
}

type FilterType = "ALL" | "TABLE" | "SP";

/**
 * Render the expert management UI for a selected project.
 *
 * Displays database entities and assigned experts, supports searching and filtering entities, and provides an interface to navigate to per-entity expert management pages. Handles loading, error, and no-project states and presents an experts summary with expertise distribution when available.
 *
 * @returns The rendered React element for the Context Experts page
 */
export default function ContextExperts() {
  const { selectedProject, selectedProjectId, hasProject } = useProject();
  const [searchQuery, setSearchQuery] = useState("");
  const [filterType, setFilterType] = useState<FilterType>("ALL");

  // Fetch all tables
  const {
    data: tablesResponse,
    isLoading: isLoadingTables,
    error: tablesError,
  } = useApi<TableMetadataDto[]>(
    `/DatabaseBrowser/projects/${selectedProjectId}/tables`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 60 * 1000,
      retry: 2,
    },
  );

  // Fetch all SPs
  const {
    data: spsResponse,
    isLoading: isLoadingSPs,
    error: spsError,
  } = useApi<StoredProcedureMetadataDto[]>(
    `/DatabaseBrowser/projects/${selectedProjectId}/stored-procedures-metadata`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 60 * 1000,
      retry: 2,
    },
  );

  // Fetch expert summary (if available)
  const { data: expertSummary, isLoading: isLoadingExperts } = useApi<
    ExpertSummary[]
  >(`/projects/${selectedProjectId}/context/experts/summary`, {
    enabled: hasProject && !!selectedProjectId,
    staleTime: 2 * 60 * 1000,
    retry: 1,
    showErrorToast: false, // This might not exist yet
  });

  const isLoading = isLoadingTables || isLoadingSPs;
  const hasError = tablesError || spsError;

  // Create entity list
  const allEntities = useMemo(() => {
    const entities: Array<{
      entityType: "TABLE" | "SP";
      entityId: number;
      entityName: string;
      schemaName?: string;
    }> = [];

    if (tablesResponse) {
      tablesResponse.forEach((table) => {
        entities.push({
          entityType: "TABLE",
          entityId: table.tableId,
          entityName: table.tableName,
          schemaName: table.schemaName,
        });
      });
    }

    if (spsResponse) {
      spsResponse.forEach((sp) => {
        entities.push({
          entityType: "SP",
          entityId: sp.spId,
          entityName: sp.procedureName,
          // schemaName: sp.schemaName,
        });
      });
    }

    return entities;
  }, [tablesResponse, spsResponse]);

  // Filter entities
  const filteredEntities = useMemo(() => {
    let filtered = allEntities;

    // Filter by search
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (e) =>
          e.entityName.toLowerCase().includes(query) ||
          e.schemaName?.toLowerCase().includes(query)
      );
    }

    // Filter by type
    if (filterType !== "ALL") {
      filtered = filtered.filter((e) => e.entityType === filterType);
    }

    return filtered;
  }, [allEntities, searchQuery, filterType]);

  // Helper functions
  const getExpertIcon = (level: string) => {
    switch (level) {
      case "OWNER":
        return <Crown className="w-4 h-4 text-yellow-500" />;
      case "EXPERT":
        return <Star className="w-4 h-4 text-blue-500" />;
      case "FAMILIAR":
        return <UserIcon className="w-4 h-4 text-gray-500" />;
      case "CONTRIBUTOR":
        return <GitCommit className="w-4 h-4 text-green-500" />;
      default:
        return <UserIcon className="w-4 h-4 text-gray-400" />;
    }
  };

  const getEntityIcon = (type: string) => {
    switch (type) {
      case "TABLE":
        return <Database className="h-4 w-4 text-green-600" />;
      case "SP":
        return <Code2 className="h-4 w-4 text-indigo-600" />;
      default:
        return <FileText className="h-4 w-4" />;
    }
  };

  const getEntityRoute = (entity: (typeof allEntities)[0]) => {
    switch (entity.entityType) {
      case "TABLE":
        return `/project/${selectedProjectId}/tables/${entity.entityId}`;
      case "SP":
        return `/project/${selectedProjectId}/stored-procedures/${entity.entityId}`;
      default:
        return `/project/${selectedProjectId}`;
    }
  };

  const getExpertBadgeVariant = (level: string) => {
    switch (level) {
      case "OWNER":
        return "default" as const;
      case "EXPERT":
        return "secondary" as const;
      case "FAMILIAR":
        return "outline" as const;
      case "CONTRIBUTOR":
        return "outline" as const;
      default:
        return "outline" as const;
    }
  };

  // Error state
  if (hasError) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load entities: {tablesError?.message || spsError?.message}
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button onClick={() => window.location.reload()} variant="outline">
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="flex flex-col items-center space-y-4">
          <Loader2 className="h-12 w-12 animate-spin text-primary" />
          <p className="text-muted-foreground">Loading entities...</p>
        </div>
      </div>
    );
  }

  // No project selected
  if (!hasProject) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to view experts.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild>
            <Link to="/projects">Select Project</Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Expert Management</h1>
          <p className="text-muted-foreground mt-1">
            Assign and manage subject matter experts for{" "}
            <span className="font-medium">{selectedProject?.projectName}</span>
          </p>
        </div>
        <div className="flex space-x-2">
          <Link to={`/projects/${selectedProjectId}/context/dashboard`}>
            <Button variant="outline">
              <ExternalLink className="w-4 h-4 mr-2" />
              Dashboard
            </Button>
          </Link>
        </div>
      </div>

      {/* Stats Cards */}
      {expertSummary && expertSummary.length > 0 && (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">
                Total Experts
              </CardTitle>
              <Users className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{expertSummary.length}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Owners</CardTitle>
              <Crown className="h-4 w-4 text-yellow-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {expertSummary.reduce(
                  (acc, expert) => acc + (expert.expertiseBreakdown.OWNER || 0),
                  0,
                )}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Experts</CardTitle>
              <Star className="h-4 w-4 text-blue-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {expertSummary.reduce(
                  (acc, expert) =>
                    acc + (expert.expertiseBreakdown.EXPERT || 0),
                  0,
                )}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">
                Total Entities
              </CardTitle>
              <Database className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{allEntities.length}</div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* How To Guide */}
      <Alert>
        <FileText className="h-4 w-4" />
        <AlertDescription>
          <div className="space-y-2">
            <p className="font-medium">How to Assign Experts:</p>
            <ol className="list-decimal list-inside space-y-1 text-sm">
              <li>
                Click "Manage Experts" on any entity below to go to its detail
                page
              </li>
              <li>Navigate to the "Context" or "Experts" tab</li>
              <li>
                Use the expert management interface to add or remove experts
              </li>
              <li>Set appropriate expertise levels based on their knowledge</li>
            </ol>
          </div>
        </AlertDescription>
      </Alert>

      <Tabs defaultValue="entities" className="space-y-4">
        <TabsList>
          <TabsTrigger value="entities">All Entities</TabsTrigger>
          {expertSummary && expertSummary.length > 0 && (
            <TabsTrigger value="experts">Experts Summary</TabsTrigger>
          )}
        </TabsList>

        {/* Entities Tab */}
        <TabsContent value="entities" className="space-y-4">
          {/* Search and Filters */}
          <Card>
            <CardContent className="pt-6">
              <div className="flex flex-col sm:flex-row gap-4">
                <div className="relative flex-1">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search entities..."
                    className="pl-10"
                  />
                </div>
                <div className="flex gap-2">
                  <Select
                    value={filterType}
                    onValueChange={(value) =>
                      setFilterType(value as FilterType)
                    }
                  >
                    <SelectTrigger className="w-32">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ALL">All Types</SelectItem>
                      <SelectItem value="TABLE">Tables</SelectItem>
                      <SelectItem value="SP">Procedures</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Entities List */}
          <Card>
            <CardHeader>
              <CardTitle>Database Entities</CardTitle>
              <CardDescription>
                {filteredEntities.length} of {allEntities.length} entities
                {searchQuery && ` matching "${searchQuery}"`}
                {filterType !== "ALL" && ` • Filtered by ${filterType}`}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {filteredEntities.length === 0 ? (
                <Alert>
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    {searchQuery
                      ? "No entities found matching your search."
                      : "No entities found in this project."}
                  </AlertDescription>
                </Alert>
              ) : (
                <div className="border rounded-lg overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Entity</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Schema</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filteredEntities.map((entity) => (
                        <TableRow
                          key={`${entity.entityType}-${entity.entityId}`}
                        >
                          <TableCell className="font-medium">
                            <div className="flex items-center gap-2">
                              {getEntityIcon(entity.entityType)}
                              <span>{entity.entityName}</span>
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline">
                              {entity.entityType === "TABLE"
                                ? "Table"
                                : "Procedure"}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {entity.schemaName || "dbo"}
                          </TableCell>
                          <TableCell className="text-right">
                            <Button variant="ghost" size="sm" asChild>
                              <Link to={getEntityRoute(entity)}>
                                Manage Experts
                              </Link>
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Experts Summary Tab */}
        {expertSummary && expertSummary.length > 0 && (
          <TabsContent value="experts" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Expert Summary</CardTitle>
                <CardDescription>
                  People with expertise across different entities
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="border rounded-lg overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Expert</TableHead>
                        <TableHead>Entities</TableHead>
                        <TableHead>Expertise Distribution</TableHead>
                        <TableHead>Contact</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {expertSummary.map((expert) => (
                        <TableRow key={expert.userId}>
                          <TableCell>
                            <div className="flex items-center gap-3">
                              <Avatar className="h-8 w-8">
                                <AvatarFallback>
                                  {(() => {
                                    const source =
                                      (expert.user.fullName?.trim() ||
                                        expert.user.username?.trim() ||
                                        "") ??
                                      "";
                                    const initials = source
                                      .split(/\s+/)
                                      .filter(Boolean)
                                      .map((word) =>
                                        word && word[0] ? word[0] : "",
                                      )
                                      .join("")
                                      .toUpperCase();

                                    return initials || ""; // fallback: empty string or could use '?'
                                  })()}
                                </AvatarFallback>
                              </Avatar>

                              <div>
                                <p className="font-medium">
                                  {expert.user.fullName || expert.user.username}
                                </p>
                                <p className="text-sm text-muted-foreground">
                                  @{expert.user.username}
                                </p>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant="secondary">
                              {expert.entityCount} entities
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <div className="flex flex-wrap gap-1">
                              {Object.entries(expert.expertiseBreakdown).map(
                                ([level, count]) =>
                                  count > 0 && (
                                    <Badge
                                      key={level}
                                      variant={getExpertBadgeVariant(level)}
                                      className="text-xs"
                                    >
                                      {getExpertIcon(level)}
                                      <span className="ml-1">{count}</span>
                                    </Badge>
                                  ),
                              )}
                            </div>
                          </TableCell>
                          <TableCell>
                            <a
                              href={`mailto:${expert.user.email}`}
                              className="flex items-center text-sm text-muted-foreground hover:text-foreground"
                            >
                              <Mail className="h-3 w-3 mr-1" />
                              {expert.user.email}
                            </a>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        )}
      </Tabs>

      {/* Expert Levels Reference */}
      <Card>
        <CardHeader>
          <CardTitle>Expert Levels Reference</CardTitle>
          <CardDescription>
            Understanding the different levels of expertise
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="flex items-start gap-3 p-3 border rounded-lg">
              <Crown className="h-5 w-5 text-yellow-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-medium">Owner</p>
                <p className="text-sm text-muted-foreground">
                  Built it, maintains it, makes decisions
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 border rounded-lg">
              <Star className="h-5 w-5 text-blue-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-medium">Expert</p>
                <p className="text-sm text-muted-foreground">
                  Deep knowledge, can train others
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 border rounded-lg">
              <UserIcon className="h-5 w-5 text-gray-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-medium">Familiar</p>
                <p className="text-sm text-muted-foreground">
                  Can answer basic questions
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 border rounded-lg">
              <GitCommit className="h-5 w-5 text-green-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-medium">Contributor</p>
                <p className="text-sm text-muted-foreground">
                  Has made changes recently
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
