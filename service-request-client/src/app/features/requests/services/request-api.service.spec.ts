import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { PagedResult, RequestDetails, RequestListItem } from '../models/request.models';
import { RequestApiService } from './request-api.service';

const requestsUrl = `${environment.apiBaseUrl}/api/requests`;

const testListItem: RequestListItem = {
  id: 1,
  title: 'Printer not working',
  status: 'New',
  priority: 'High',
  category: { id: 1, name: 'Hardware' },
  createdBy: { id: 3, displayName: 'Development Employee' },
  assignedTo: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const testPagedResult: PagedResult<RequestListItem> = {
  items: [testListItem],
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
};

const testDetails: RequestDetails = {
  ...testListItem,
  description: 'The printer jams every time it is used.',
  resolvedAt: null,
  closedAt: null,
  cancelledAt: null,
};

describe('RequestApiService', () => {
  let service: RequestApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(RequestApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('issues a default list request with page 1 and pageSize 20', () => {
    service.getRequests().subscribe();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush(testPagedResult);
  });

  it('passes custom pagination parameters', () => {
    service.getRequests({ page: 3, pageSize: 10 }).subscribe();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush(testPagedResult);
  });

  it('passes status, priority, and category filters', () => {
    service.getRequests({ status: 'New', priority: 'High', categoryId: 5 }).subscribe();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.get('status')).toBe('New');
    expect(req.request.params.get('priority')).toBe('High');
    expect(req.request.params.get('categoryId')).toBe('5');
    req.flush(testPagedResult);
  });

  it('omits filter parameters when not provided', () => {
    service.getRequests().subscribe();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.has('status')).toBeFalse();
    expect(req.request.params.has('priority')).toBeFalse();
    expect(req.request.params.has('categoryId')).toBeFalse();
    req.flush(testPagedResult);
  });

  it('retrieves request details by id', () => {
    service.getRequest(1).subscribe((details) => {
      expect(details).toEqual(testDetails);
    });

    const req = httpMock.expectOne(`${requestsUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(testDetails);
  });

  it('creates a request with the correct body', () => {
    service
      .createRequest({ title: 'Laptop broken', description: 'It will not turn on.', categoryId: 2, priority: 'High' })
      .subscribe();

    const req = httpMock.expectOne(requestsUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      title: 'Laptop broken',
      description: 'It will not turn on.',
      categoryId: 2,
      priority: 'High',
    });
    req.flush(testDetails);
  });
});
