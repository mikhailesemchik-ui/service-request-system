export const DASHBOARD_SCOPES = ['Employee', 'SupportAgent', 'Admin'] as const;

export type DashboardScope = (typeof DASHBOARD_SCOPES)[number];

export interface DashboardStatusCount {
  status: string;
  count: number;
}

export interface DashboardPriorityCount {
  priority: string;
  count: number;
}

export interface DashboardRecentRequest {
  id: number;
  title: string;
  status: string;
  priority: string;
  categoryName: string;
  createdByDisplayName: string;
  assignedToDisplayName: string | null;
  updatedAt: string;
}

export interface DashboardStaffMetrics {
  unassignedActiveRequests: number;
  assignedToMe: number;
  activeAssignedToMe: number;
}

export interface DashboardAdminMetrics {
  activeCategories: number;
  activeSupportAgents: number;
  activeAdmins: number;
}

export interface DashboardSummary {
  scope: DashboardScope;
  totalRequests: number;
  openRequests: number;
  resolvedRequests: number;
  closedRequests: number;
  cancelledRequests: number;
  statusCounts: DashboardStatusCount[];
  priorityCounts: DashboardPriorityCount[];
  staffMetrics: DashboardStaffMetrics | null;
  adminMetrics: DashboardAdminMetrics | null;
  recentRequests: DashboardRecentRequest[];
}

export const DASHBOARD_SUMMARY_PATH = '/api/dashboard/summary';
