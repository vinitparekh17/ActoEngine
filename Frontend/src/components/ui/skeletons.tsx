import { Skeleton } from "./skeleton";
export { Skeleton };

// ============================================
// Table Skeleton
// ============================================
interface TableSkeletonProps {
  rows?: number;
  columns?: number;
}

export function TableSkeleton({ rows = 5, columns = 4 }: TableSkeletonProps) {
  return (
    <div className="space-y-3">
      {/* Table Header Skeleton */}
      <div className="flex space-x-4">
        {Array.from({ length: columns }).map((_, i) => (
          <Skeleton key={`header-${i}`} className="h-8 flex-1" />
        ))}
      </div>

      {/* Table Rows Skeleton */}
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div key={`row-${rowIndex}`} className="flex space-x-4">
          {Array.from({ length: columns }).map((_, colIndex) => (
            <Skeleton
              key={`cell-${rowIndex}-${colIndex}`}
              className="h-12 flex-1"
            />
          ))}
        </div>
      ))}
    </div>
  );
}

// ============================================
// Card Skeleton
// ============================================
interface CardSkeletonProps {
  showAvatar?: boolean;
  lines?: number;
}

export function CardSkeleton({
  showAvatar = false,
  lines = 3,
}: CardSkeletonProps) {
  return (
    <div className="space-y-3 p-4 border rounded-lg">
      {showAvatar && (
        <div className="flex items-center space-x-3">
          <Skeleton className="h-10 w-10 rounded-full" />
          <div className="space-y-2 flex-1">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
          </div>
        </div>
      )}

      <div className="space-y-2">
        {Array.from({ length: lines }).map((_, i) => (
          <Skeleton
            key={i}
            className={`h-4 ${i === lines - 1 ? "w-2/3" : "w-full"}`}
          />
        ))}
      </div>
    </div>
  );
}

// ============================================
// Form Skeleton
// ============================================
interface FormSkeletonProps {
  fields?: number;
}

export function FormSkeleton({ fields = 3 }: FormSkeletonProps) {
  return (
    <div className="space-y-6">
      {Array.from({ length: fields }).map((_, i) => (
        <div key={i} className="space-y-2">
          <Skeleton className="h-4 w-20" /> {/* Label */}
          <Skeleton className="h-10 w-full" /> {/* Input */}
        </div>
      ))}
      <Skeleton className="h-10 w-32" /> {/* Button */}
    </div>
  );
}

// ============================================
// List Skeleton
// ============================================
interface ListSkeletonProps {
  items?: number;
  showAvatar?: boolean;
}

export function ListSkeleton({
  items = 5,
  showAvatar = false,
}: ListSkeletonProps) {
  return (
    <div className="space-y-4">
      {Array.from({ length: items }).map((_, i) => (
        <div key={i} className="flex items-center space-x-3">
          {showAvatar && <Skeleton className="h-8 w-8 rounded-full" />}
          <div className="space-y-2 flex-1">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-3 w-2/3" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ============================================
// Page Header Skeleton
// ============================================
export function PageHeaderSkeleton() {
  return (
    <div className="space-y-2 mb-6">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-4 w-96" />
    </div>
  );
}

// ============================================
// Generic Loading Container
// ============================================
interface LoadingContainerProps {
  children: React.ReactNode;
  isLoading: boolean;
  skeleton: React.ReactNode;
  className?: string;
}

export function LoadingContainer({
  children,
  isLoading,
  skeleton,
  className = "",
}: LoadingContainerProps) {
  if (isLoading) {
    return <div className={className}>{skeleton}</div>;
  }

  return <>{children}</>;
}

// ============================================
// Grid Skeleton
// ============================================
interface GridSkeletonProps {
  count?: number;
  skeleton?: React.ReactNode;
  className?: string;
}

export function GridSkeleton({
  count = 6,
  skeleton = <CardSkeleton />,
  className = "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6",
}: GridSkeletonProps) {
  return (
    <div className={className}>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i}>{skeleton}</div>
      ))}
    </div>
  );
}
