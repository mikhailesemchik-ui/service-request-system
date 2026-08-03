import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { DashboardSummary } from '../models/dashboard.models';
import { DashboardApiService } from './dashboard-api.service';

const dashboardUrl = `${environment.apiBaseUrl}/api/dashboard/summary`;

const emptySummary: DashboardSummary = {
  scope: 'Employee',
  totalRequests: 0,
  openRequests: 0,
  resolvedRequests: 0,
  closedRequests: 0,
  cancelledRequests: 0,
  statusCounts: [],
  priorityCounts: [],
  staffMetrics: null,
  adminMetrics: null,
  recentRequests: [],
};

describe('DashboardApiService', () => {
  let service: DashboardApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getSummary sends GET to the correct URL', () => {
    service.getSummary().subscribe();

    const req = httpMock.expectOne(dashboardUrl);
    expect(req.request.method).toBe('GET');
    req.flush(emptySummary);
  });

  it('getSummary sends no query parameters', () => {
    service.getSummary().subscribe();

    const req = httpMock.expectOne(dashboardUrl);
    expect(req.request.params.keys().length).toBe(0);
    req.flush(emptySummary);
  });

  it('getSummary returns deserialized DashboardSummary', () => {
    const expected: DashboardSummary = {
      ...emptySummary,
      scope: 'Admin',
      totalRequests: 5,
      openRequests: 3,
      adminMetrics: { activeCategories: 2, activeSupportAgents: 1, activeAdmins: 1 },
    };

    let result: DashboardSummary | undefined;
    service.getSummary().subscribe((s) => (result = s));

    httpMock.expectOne(dashboardUrl).flush(expected);

    expect(result).toEqual(expected);
  });
});
