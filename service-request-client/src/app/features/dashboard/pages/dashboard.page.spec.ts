import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { DashboardAdminMetrics, DashboardStaffMetrics, DashboardSummary } from '../models/dashboard.models';
import { DashboardPageComponent } from './dashboard.page';

const dashboardUrl = `${environment.apiBaseUrl}/api/dashboard/summary`;

const noStaff: DashboardStaffMetrics | null = null;
const noAdmin: DashboardAdminMetrics | null = null;

const emptySummary: DashboardSummary = {
  scope: 'Employee',
  totalRequests: 0,
  openRequests: 0,
  resolvedRequests: 0,
  closedRequests: 0,
  cancelledRequests: 0,
  statusCounts: [
    { status: 'New', count: 0 },
    { status: 'InProgress', count: 0 },
    { status: 'WaitingForUser', count: 0 },
    { status: 'Resolved', count: 0 },
    { status: 'Closed', count: 0 },
    { status: 'Cancelled', count: 0 },
  ],
  priorityCounts: [
    { priority: 'Low', count: 0 },
    { priority: 'Medium', count: 0 },
    { priority: 'High', count: 0 },
    { priority: 'Critical', count: 0 },
  ],
  staffMetrics: noStaff,
  adminMetrics: noAdmin,
  recentRequests: [],
};

const staffSummary: DashboardSummary = {
  ...emptySummary,
  scope: 'SupportAgent',
  totalRequests: 10,
  openRequests: 4,
  staffMetrics: {
    unassignedActiveRequests: 2,
    assignedToMe: 3,
    activeAssignedToMe: 2,
  },
  adminMetrics: noAdmin,
};

const adminSummary: DashboardSummary = {
  ...staffSummary,
  scope: 'Admin',
  adminMetrics: {
    activeCategories: 5,
    activeSupportAgents: 3,
    activeAdmins: 1,
  },
};

describe('DashboardPageComponent', () => {
  let fixture: ComponentFixture<DashboardPageComponent>;
  let httpMock: HttpTestingController;

  function setup(role: UserRole = 'Employee', userId = 1): void {
    TestBed.configureTestingModule({
      imports: [DashboardPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: () => ({
              id: userId,
              username: 'test.user',
              displayName: 'Test User',
              email: 'test@example.test',
              role,
            }),
            hasRole: (r: UserRole) => r === role,
            hasAnyRole: (roles: UserRole[]) => roles.includes(role),
          },
        },
      ],
    });

    fixture = TestBed.createComponent(DashboardPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
  }

  function flush(summary: DashboardSummary = emptySummary): void {
    httpMock.expectOne(dashboardUrl).flush(summary);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ── Loading / error / retry ────────────────────────────────────────────────

  it('shows loading state before response arrives', () => {
    setup();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="status"]')).toBeTruthy();
    expect(el.querySelector('[role="alert"]')).toBeNull();
    httpMock.expectOne(dashboardUrl).flush(emptySummary);
  });

  it('hides loading state after response', () => {
    setup();
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="status"]')).toBeNull();
  });

  it('shows error state when request fails', () => {
    setup();
    fixture.detectChanges();
    httpMock.expectOne(dashboardUrl).error(new ProgressEvent('error'), { status: 500 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).toBeTruthy();
    expect(el.querySelector('[role="status"]')).toBeNull();
  });

  it('retry button reloads data', () => {
    setup();
    fixture.detectChanges();
    httpMock.expectOne(dashboardUrl).error(new ProgressEvent('error'), { status: 500 });
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    flush(); // second request
  });

  it('does not show stale metric values during initial loading', () => {
    setup();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const cardValues = el.querySelectorAll('.dashboard-page__card-value');
    expect(cardValues.length).toBe(0);

    httpMock.expectOne(dashboardUrl).flush(emptySummary);
  });

  // ── Employee scope ────────────────────────────────────────────────────────

  it('Employee - renders total, open, resolved, closed, cancelled cards', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      totalRequests: 8,
      openRequests: 3,
      resolvedRequests: 2,
      closedRequests: 2,
      cancelledRequests: 1,
    });

    const el = fixture.nativeElement as HTMLElement;
    const cardValues = Array.from(el.querySelectorAll('.dashboard-page__card-value')).map(
      (e) => e.textContent?.trim(),
    );
    expect(cardValues).toContain('8');
    expect(cardValues).toContain('3');
    expect(cardValues).toContain('2');
    expect(cardValues).toContain('1');
  });

  it('Employee - does not render staff metrics section', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#staff-heading')).toBeNull();
  });

  it('Employee - does not render admin metrics section', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#admin-heading')).toBeNull();
  });

  it('Employee - openRequests label is "Open"', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Open');
    expect(labels).not.toContain('Active');
  });

  it('Employee - recent requests render own items', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      recentRequests: [
        {
          id: 7,
          title: 'My request',
          status: 'New',
          priority: 'High',
          categoryName: 'Hardware',
          createdByDisplayName: 'Test User',
          assignedToDisplayName: null,
          updatedAt: '2026-01-01T10:00:00Z',
        },
      ],
    });

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dashboard-page__recent')).toBeTruthy();
    expect(el.querySelectorAll('.dashboard-page__recent-item').length).toBe(1);
    expect(el.querySelector('.dashboard-page__recent-title')?.textContent?.trim()).toBe('My request');
  });

  it('Employee - empty recent state renders message', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dashboard-page__empty')).toBeTruthy();
  });

  // ── SupportAgent scope ────────────────────────────────────────────────────

  it('SupportAgent - operational cards render', () => {
    setup('SupportAgent');
    fixture.detectChanges();
    flush(staffSummary);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#staff-heading')).toBeTruthy();
    const cardValues = Array.from(el.querySelectorAll('.dashboard-page__card-value')).map(
      (e) => e.textContent?.trim(),
    );
    expect(cardValues).toContain('2'); // unassignedActiveRequests
    expect(cardValues).toContain('3'); // assignedToMe
  });

  it('SupportAgent - assigned-to-me values render', () => {
    setup('SupportAgent');
    fixture.detectChanges();
    flush(staffSummary);

    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Assigned to me');
    expect(labels).toContain('Active assigned to me');
  });

  it('SupportAgent - admin organisation cards do not render', () => {
    setup('SupportAgent');
    fixture.detectChanges();
    flush(staffSummary);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#admin-heading')).toBeNull();
  });

  it('SupportAgent - openRequests label is "Active"', () => {
    setup('SupportAgent');
    fixture.detectChanges();
    flush(staffSummary);

    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Active');
    expect(labels).not.toContain('Open');
  });

  // ── Admin scope ───────────────────────────────────────────────────────────

  it('Admin - operational cards render', () => {
    setup('Admin');
    fixture.detectChanges();
    flush(adminSummary);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#staff-heading')).toBeTruthy();
  });

  it('Admin - active categories card renders', () => {
    setup('Admin');
    fixture.detectChanges();
    flush(adminSummary);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#admin-heading')).toBeTruthy();
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Active categories');
  });

  it('Admin - active support agents card renders', () => {
    setup('Admin');
    fixture.detectChanges();
    flush(adminSummary);

    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Active support agents');
  });

  it('Admin - active admins card renders', () => {
    setup('Admin');
    fixture.detectChanges();
    flush(adminSummary);

    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.dashboard-page__card-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(labels).toContain('Active admins');
  });

  // ── Breakdowns ────────────────────────────────────────────────────────────

  it('every status renders in breakdown', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    const items = Array.from(el.querySelectorAll('.dashboard-page__breakdown-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(items).toContain('New');
    expect(items).toContain('In Progress');
    expect(items).toContain('Waiting for User');
    expect(items).toContain('Resolved');
    expect(items).toContain('Closed');
    expect(items).toContain('Cancelled');
  });

  it('every priority renders in breakdown', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    const items = Array.from(el.querySelectorAll('.dashboard-page__breakdown-label')).map(
      (e) => e.textContent?.trim(),
    );
    expect(items).toContain('Low');
    expect(items).toContain('Medium');
    expect(items).toContain('High');
    expect(items).toContain('Critical');
  });

  it('zero counts render visibly', () => {
    setup('Employee');
    fixture.detectChanges();
    flush();

    const el = fixture.nativeElement as HTMLElement;
    const counts = Array.from(el.querySelectorAll('.dashboard-page__breakdown-count')).map(
      (e) => e.textContent?.trim(),
    );
    expect(counts).toContain('0');
    expect(counts.length).toBe(10); // 6 statuses + 4 priorities
  });

  it('counts display correctly', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      totalRequests: 3,
      statusCounts: [
        { status: 'New', count: 3 },
        { status: 'InProgress', count: 0 },
        { status: 'WaitingForUser', count: 0 },
        { status: 'Resolved', count: 0 },
        { status: 'Closed', count: 0 },
        { status: 'Cancelled', count: 0 },
      ],
      priorityCounts: emptySummary.priorityCounts,
    });

    const el = fixture.nativeElement as HTMLElement;
    const counts = Array.from(el.querySelectorAll('.dashboard-page__breakdown-count')).map(
      (e) => e.textContent?.trim(),
    );
    expect(counts[0]).toBe('3');
  });

  it('zero-total does not produce invalid percentage output', () => {
    setup('Employee');
    fixture.detectChanges();
    flush(); // totalRequests = 0

    const el = fixture.nativeElement as HTMLElement;
    const bars = Array.from(el.querySelectorAll('.dashboard-page__breakdown-bar'));
    bars.forEach((bar) => {
      const width = (bar as HTMLElement).style.width;
      // Must be '0%' or empty – must never be 'NaN%' or 'Infinity%'
      expect(width).not.toContain('NaN');
      expect(width).not.toContain('Infinity');
    });
  });

  // ── Recent requests ───────────────────────────────────────────────────────

  it('at most five recent requests render', () => {
    setup('Employee');
    fixture.detectChanges();
    const manyRecent = Array.from({ length: 5 }, (_, i) => ({
      id: i + 1,
      title: `Request ${i + 1}`,
      status: 'New',
      priority: 'Low',
      categoryName: 'Cat',
      createdByDisplayName: 'User',
      assignedToDisplayName: null,
      updatedAt: '2026-01-01T00:00:00Z',
    }));
    flush({ ...emptySummary, recentRequests: manyRecent });

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.dashboard-page__recent-item').length).toBeLessThanOrEqual(5);
  });

  it('recent request renders title and ID', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      recentRequests: [
        {
          id: 42,
          title: 'Broken printer',
          status: 'New',
          priority: 'Medium',
          categoryName: 'Hardware',
          createdByDisplayName: 'Alice',
          assignedToDisplayName: null,
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ],
    });

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dashboard-page__recent-id')?.textContent).toContain('42');
    expect(el.querySelector('.dashboard-page__recent-title')?.textContent?.trim()).toBe('Broken printer');
  });

  it('recent request renders status, priority, category, and assignee', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      recentRequests: [
        {
          id: 1,
          title: 'Test',
          status: 'InProgress',
          priority: 'High',
          categoryName: 'Software',
          createdByDisplayName: 'Bob',
          assignedToDisplayName: 'Agent Smith',
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ],
    });

    const el = fixture.nativeElement as HTMLElement;
    const text = el.querySelector('.dashboard-page__recent-item')!.textContent ?? '';
    expect(text).toContain('In Progress');
    expect(text).toContain('High');
    expect(text).toContain('Software');
    expect(text).toContain('Agent Smith');
  });

  it('unassigned fallback renders when assignedToDisplayName is null', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      recentRequests: [
        {
          id: 1,
          title: 'Test',
          status: 'New',
          priority: 'Low',
          categoryName: 'Hardware',
          createdByDisplayName: 'Bob',
          assignedToDisplayName: null,
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ],
    });

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dashboard-page__recent-item')!.textContent).toContain('Unassigned');
  });

  it('links point to correct request details routes', () => {
    setup('Employee');
    fixture.detectChanges();
    flush({
      ...emptySummary,
      recentRequests: [
        {
          id: 99,
          title: 'Linked request',
          status: 'New',
          priority: 'Low',
          categoryName: 'Hardware',
          createdByDisplayName: 'Alice',
          assignedToDisplayName: null,
          updatedAt: '2026-01-01T00:00:00Z',
        },
      ],
    });

    const el = fixture.nativeElement as HTMLElement;
    const link = el.querySelector<HTMLAnchorElement>('.dashboard-page__recent-link');
    expect(link?.getAttribute('href')).toBe('/requests/99');
  });
});
