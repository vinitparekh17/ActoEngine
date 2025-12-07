import { useAuth } from "../hooks/useAuth";
import { Zap, FolderOpen, Clock, CheckCircle } from "lucide-react";

export default function DashboardPage() {
  const { user } = useAuth();

  const stats = [
    {
      label: "Total Projects",
      value: "12",
      icon: FolderOpen,
      color: "bg-blue-500",
    },
    {
      label: "SPs Generated",
      value: "248",
      icon: Zap,
      color: "bg-green-500",
    },
    {
      label: "This Week",
      value: "42",
      icon: Clock,
      color: "bg-purple-500",
    },
    {
      label: "Success Rate",
      value: "98%",
      icon: CheckCircle,
      color: "bg-emerald-500",
    },
  ];

  return (
    <div className="space-y-6">
      {/* Welcome Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">
          Welcome back, {user?.username}! 👋
        </h1>
        <p className="mt-2 text-gray-600">
          Here's your stored procedure generation overview
        </p>
      </div>

      {/* Stats Grid */}
      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="rounded-2xl border bg-white p-6 shadow-sm"
          >
            <div className="flex items-center gap-4">
              <div className={`rounded-xl ${stat.color} p-3`}>
                <stat.icon className="h-6 w-6 text-white" />
              </div>
              <div>
                <div className="text-2xl font-bold text-gray-900">
                  {stat.value}
                </div>
                <div className="text-sm text-gray-600">{stat.label}</div>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Quick Actions */}
      <div className="rounded-2xl border bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-lg font-semibold text-gray-900">
          Quick Actions
        </h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <button className="rounded-xl border border-blue-200 bg-blue-50 p-4 text-left transition-colors hover:bg-blue-100">
            <div className="text-sm font-medium text-blue-900">
              Generate New SP
            </div>
            <div className="mt-1 text-xs text-blue-600">
              Create CUD or SELECT stored procedures
            </div>
          </button>

          <button className="rounded-xl border border-purple-200 bg-purple-50 p-4 text-left transition-colors hover:bg-purple-100">
            <div className="text-sm font-medium text-purple-900">
              Browse Projects
            </div>
            <div className="mt-1 text-xs text-purple-600">
              View and manage database projects
            </div>
          </button>

          <button className="rounded-xl border border-green-200 bg-green-50 p-4 text-left transition-colors hover:bg-green-100">
            <div className="text-sm font-medium text-green-900">
              View History
            </div>
            <div className="mt-1 text-xs text-green-600">
              Check previously generated SPs
            </div>
          </button>
        </div>
      </div>

      {/* Recent Activity */}
      <div className="rounded-2xl border bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-lg font-semibold text-gray-900">
          Recent Activity
        </h2>
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="flex items-center gap-4 rounded-lg border p-3"
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50">
                <Zap className="h-5 w-5 text-blue-600" />
              </div>
              <div className="flex-1">
                <div className="text-sm font-medium text-gray-900">
                  Generated CUD SP for Users table
                </div>
                <div className="text-xs text-gray-500">2 hours ago</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
