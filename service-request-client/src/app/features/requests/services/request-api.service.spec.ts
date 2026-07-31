import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { PagedResult, RequestAssignee, RequestDetails, RequestHistoryItem, RequestListItem } from '../models/request.models';
import { RequestApiService } from './request-api.service';

const requestsUrl = `${environment.apiBaseUrl}/api/requests`;
const requestAssigneesUrl = `${environment.apiBaseUrl}/api/request-assignees`;

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

  it('sends the assignment PATCH body', () => {
    service.setAssignment(1, { assignedToUserId: 4 }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/1/assignment`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ assignedToUserId: 4 });
    req.flush(testDetails);
  });

  it('sends a null assignee to remove an assignment', () => {
    service.setAssignment(1, { assignedToUserId: null }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/1/assignment`);
    expect(req.request.body).toEqual({ assignedToUserId: null });
    req.flush(testDetails);
  });

  it('sends the status PATCH body', () => {
    service.changeStatus(1, { status: 'InProgress' }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/1/status`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'InProgress' });
    req.flush(testDetails);
  });

  it('retrieves request history', () => {
    const historyItem: RequestHistoryItem = {
      id: 1,
      action: 'StatusChanged',
      previousValue: 'New',
      newValue: 'InProgress',
      previousDisplayValue: 'New',
      newDisplayValue: 'InProgress',
      changedBy: { id: 3, displayName: 'Development Employee' },
      createdAt: '2026-01-01T00:00:00Z',
    };

    service.getHistory(1).subscribe((history) => {
      expect(history).toEqual([historyItem]);
    });

    const req = httpMock.expectOne(`${requestsUrl}/1/history`);
    expect(req.request.method).toBe('GET');
    req.flush([historyItem]);
  });

  it('retrieves eligible assignees', () => {
    const assignee: RequestAssignee = { id: 4, displayName: 'Development Agent', role: 'SupportAgent' };

    service.getEligibleAssignees().subscribe((assignees) => {
      expect(assignees).toEqual([assignee]);
    });

    const req = httpMock.expectOne(requestAssigneesUrl);
    expect(req.request.method).toBe('GET');
    req.flush([assignee]);
  });

  it('sends the classification PATCH to the correct URL', () => {
    service.updateClassification(42, { categoryId: 2, priority: 'High' }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/42/classification`);
    expect(req.request.method).toBe('PATCH');
    req.flush(testDetails);
  });

  it('sends exactly categoryId and priority in the classification body', () => {
    service.updateClassification(42, { categoryId: 3, priority: 'Critical' }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/42/classification`);
    expect(req.request.body).toEqual({ categoryId: 3, priority: 'Critical' });
    req.flush(testDetails);
  });

  it('returns the updated RequestDetails from classification', () => {
    const updatedDetails: RequestDetails = { ...testDetails, priority: 'Critical' };

    service.updateClassification(42, { categoryId: 1, priority: 'Critical' }).subscribe((result) => {
      expect(result).toEqual(updatedDetails);
    });

    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(updatedDetails);
  });

  it('does not include unrelated fields in the classification body', () => {
    service.updateClassification(42, { categoryId: 2, priority: 'Low' }).subscribe();

    const req = httpMock.expectOne(`${requestsUrl}/42/classification`);
    const bodyKeys = Object.keys(req.request.body as object);
    expect(bodyKeys).toEqual(jasmine.arrayWithExactContents(['categoryId', 'priority']));
    req.flush(testDetails);
  });
});
