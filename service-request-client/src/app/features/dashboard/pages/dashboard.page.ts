import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { DashboardSummary } from '../models/dashboard.models';
import { DashboardApiService } from '../services/dashboard-api.service';

const LOAD_ERROR_MESSAGE = 'Unable to load dashboard statistics. Please try again.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';

const STATUS_LABELS: Record<string, string> = {
  New: 'New',
  InProgress: 'In Progress',
  WaitingForUser: 'Waiting for User',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
};

const PRIORITY_LABELS: Record<string, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
};

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPageComponent {
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  protected readonly isEmployee = computed(() => this.authService.hasRole('Employee'));
  protected readonly isStaff = computed(() => this.authService.hasAnyRole(['SupportAgent', 'Admin']));
  protected readonly isAdmin = computed(() => this.authService.hasRole('Admin'));

  protected readonly openLabel = computed(() =>
    this.isEmployee() ? 'Open' : 'Active',
  );

  protected readonly scopeDescription = computed(() => {
    const scope = this.summary()?.scope;
    if (scope === 'Employee') return 'Showing statistics for your own requests.';
    if (scope === 'SupportAgent') return 'Showing operational statistics across all requests.';
    if (scope === 'Admin') return 'Showing operational statistics across all requests and organisation metrics.';
    return '';
  });

  constructor() {
    this.loadSummary();
  }

  protected retry(): void {
    this.loadSummary();
  }

  protected statusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  protected priorityLabel(priority: string): string {
    return PRIORITY_LABELS[priority] ?? priority;
  }

  private loadSummary(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.dashboardApi
      .getSummary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.summary.set(data);
          this.isLoading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.isLoading.set(false);
          this.loadError.set(err.status === 0 ? SERVICE_UNAVAILABLE_MESSAGE : LOAD_ERROR_MESSAGE);
        },
      });
  }
}
