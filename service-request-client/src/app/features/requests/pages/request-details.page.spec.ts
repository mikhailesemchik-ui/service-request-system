import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { RequestAssignee, RequestDetails, RequestHistoryItem } from '../models/request.models';
import { RequestDetailsPageComponent } from './request-details.page';

const requestsUrl = `${environment.apiBaseUrl}/api/requests`;
const requestAssigneesUrl = `${environment.apiBaseUrl}/api/request-assignees`;

const testDetails: RequestDetails = {
  id: 42,
  title: 'Laptop does not start',
  description: 'The power button does not respond at all.',
  status: 'Resolved',
  priority: 'High',
  category: { id: 1, name: 'Hardware' },
  createdBy: { id: 3, displayName: 'Development Employee' },
  assignedTo: { id: 4, displayName: 'Support Agent' },
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-02T00:00:00Z',
  resolvedAt: '2026-01-02T00:00:00Z',
  closedAt: null,
  cancelledAt: null,
};

const unassignedNewRequest: RequestDetails = {
  ...testDetails,
  status: 'New',
  assignedTo: null,
  resolvedAt: null,
};

const assignedToAgentInProgress: RequestDetails = {
  ...testDetails,
  status: 'InProgress',
  assignedTo: { id: 4, displayName: 'Support Agent' },
  resolvedAt: null,
};

describe('RequestDetailsPageComponent', () => {
  let fixture: ComponentFixture<RequestDetailsPageComponent>;
  let httpMock: HttpTestingController;
  let router: Router;
  let currentRole: UserRole;

  function createFixture(role: UserRole = 'Employee', userId = 3, requestId: string | null = '42'): void {
    currentRole = role;

    TestBed.configureTestingModule({
      imports: [RequestDetailsPageComponent],
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
              email: 'test.user@example.test',
              role,
            }),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(requestId ? { requestId } : {}) } },
        },
      ],
    });

    fixture = TestBed.createComponent(RequestDetailsPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  }

  function flushDetails(
    details: RequestDetails = testDetails,
    historyItems: RequestHistoryItem[] = [],
    assignees: RequestAssignee[] = [],
  ): void {
    httpMock.expectOne(`${requestsUrl}/42`).flush(details);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush(historyItems);
    if (currentRole === 'Admin') {
      httpMock.expectOne(requestAssigneesUrl).flush(assignees);
    }
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a loading state before the request resolves', () => {
    createFixture();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading request');

    flushDetails();
  });

  it('renders request data once loaded', () => {
    createFixture();
    fixture.detectChanges();
    flushDetails();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Laptop does not start');
    expect(fixture.nativeElement.textContent).toContain('Support Agent');
    expect(fixture.nativeElement.textContent).toContain('The power button does not respond at all.');
  });

  it('shows an Unassigned fallback when there is no assignee', () => {
    createFixture();
    fixture.detectChanges();
    flushDetails({ ...testDetails, assignedTo: null });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unassigned');
  });

  it('renders lifecycle timestamps only when present', () => {
    createFixture();
    fixture.detectChanges();
    flushDetails();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Resolved');
    expect(fixture.nativeElement.textContent).not.toContain('Closed');
    expect(fixture.nativeElement.textContent).not.toContain('Cancelled');
  });

  it('shows an invalid-id message without calling the API for a non-numeric id', () => {
    createFixture('Employee', 3, 'not-a-number');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('This request could not be found.');
    httpMock.expectNone(() => true);
  });

  it('shows a 404 message when the request is missing or inaccessible', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('This request does not exist or is not available to you.');
  });

  it('retries loading on request', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector('.request-details-page__status button');
    retryButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42`);
    expect(req.request.method).toBe('GET');
    req.flush(testDetails);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('navigates back to the requests list', () => {
    createFixture();
    fixture.detectChanges();
    flushDetails();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__back').click();

    expect(router.navigate).toHaveBeenCalledWith(['/requests']);
  });

  // Assignment UI

  it('shows no assignment controls for an Employee', () => {
    createFixture('Employee');
    fixture.detectChanges();
    flushDetails();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#assignment-heading')).toBeNull();
  });

  it('shows "Assign to me" for a SupportAgent when the request is unassigned', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__assignment-controls button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Assign to me');
  });

  it('shows "Remove my assignment" for a SupportAgent assigned to themselves', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__assignment-controls button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Remove my assignment');
  });

  it('shows no takeover control for a SupportAgent when assigned to another user', () => {
    createFixture('SupportAgent', 999);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const controls = fixture.nativeElement.querySelector('.request-details-page__assignment-controls');
    expect(controls?.querySelectorAll('button').length ?? 0).toBe(0);
  });

  it('loads eligible assignees only for Admin', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);

    const req = httpMock.expectOne(requestAssigneesUrl);
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 4, displayName: 'Support Agent', role: 'SupportAgent' }]);
  });

  it('does not load eligible assignees for a SupportAgent', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();

    flushDetails(unassignedNewRequest);

    httpMock.expectNone(requestAssigneesUrl);
    expect(fixture.nativeElement.querySelector('#assignee-select')).toBeNull();
  });

  it('Admin can assign the selected assignee', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [{ id: 4, displayName: 'Support Agent', role: 'SupportAgent' }]);
    fixture.detectChanges();

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('#assignee-select');
    select.value = '4';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const assignButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__assignment-controls button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.trim() === 'Assign') as HTMLButtonElement;
    assignButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/assignment`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ assignedToUserId: 4 });
    req.flush({ ...unassignedNewRequest, assignedTo: { id: 4, displayName: 'Support Agent' } });

    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('Admin can reassign to a different assignee', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], [{ id: 5, displayName: 'Second Agent', role: 'SupportAgent' }]);
    fixture.detectChanges();

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('#assignee-select');
    select.value = '5';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const reassignButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__assignment-controls button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.trim() === 'Reassign') as HTMLButtonElement;
    reassignButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/assignment`);
    expect(req.request.body).toEqual({ assignedToUserId: 5 });
    req.flush({ ...assignedToAgentInProgress, assignedTo: { id: 5, displayName: 'Second Agent' } });

    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('Admin can remove the assignment', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const removeButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__assignment-controls button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.trim() === 'Remove assignment') as HTMLButtonElement;
    removeButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/assignment`);
    expect(req.request.body).toEqual({ assignedToUserId: null });
    req.flush({ ...assignedToAgentInProgress, assignedTo: null });

    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('preserves the displayed request when an assignment write fails', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__assignment-controls button').click();
    httpMock
      .expectOne(`${requestsUrl}/42/assignment`)
      .flush({ detail: 'This request is assigned to another support agent.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('This request is assigned to another support agent.');
    expect(fixture.nativeElement.textContent).toContain('Unassigned');
  });

  // Status UI

  it('shows Cancel for an Employee on a New request', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__status-actions button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Cancel request');
  });

  it('shows Close for an Employee only when the request is Resolved', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__status-actions button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Close request');
  });

  it('shows no status actions for a SupportAgent when unassigned', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#status-heading')).toBeNull();
  });

  it('shows status actions for a SupportAgent when assigned to themselves', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__status-actions button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Resolve');
    expect(labels).not.toContain('Close request');
  });

  it('shows every valid transition for an Admin and never an invalid one', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], []);
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.request-details-page__status-actions button',
    );
    const labels = Array.from(buttons).map((button) => button.textContent?.trim());
    expect(labels).toContain('Wait for user');
    expect(labels).toContain('Resolve');
    expect(labels).toContain('Cancel request');
  });

  it('requires confirmation before cancelling', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    const cancelButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.request-details-page__status-actions button',
    );
    cancelButton.click();
    fixture.detectChanges();

    httpMock.expectNone(`${requestsUrl}/42/status`);
    expect(fixture.nativeElement.querySelector('.confirm')).not.toBeNull();
  });

  it('confirms cancellation and calls the API', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__status-actions button').click();
    fixture.detectChanges();
    const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('.confirm button:first-of-type');
    confirmButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/status`);
    expect(req.request.body).toEqual({ status: 'Cancelled' });
    req.flush({ ...unassignedNewRequest, status: 'Cancelled', cancelledAt: '2026-01-03T00:00:00Z' });

    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('requires confirmation before closing', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__status-actions button').click();
    fixture.detectChanges();

    httpMock.expectNone(`${requestsUrl}/42/status`);
    expect(fixture.nativeElement.querySelector('.confirm')).not.toBeNull();
  });

  it('prevents a duplicate status mutation while saving', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const resolveButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__status-actions button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.trim() === 'Resolve') as HTMLButtonElement;
    resolveButton.click();
    resolveButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/status`);
    expect(req.request.body).toEqual({ status: 'Resolved' });
    req.flush({ ...assignedToAgentInProgress, status: 'Resolved', resolvedAt: '2026-01-03T00:00:00Z' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('replaces the displayed request with the successful response', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    const resolveButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__status-actions button'),
    ).find((button) => (button as HTMLButtonElement).textContent?.trim() === 'Resolve') as HTMLButtonElement;
    resolveButton.click();

    httpMock
      .expectOne(`${requestsUrl}/42/status`)
      .flush({ ...assignedToAgentInProgress, status: 'Resolved', resolvedAt: '2026-01-03T00:00:00Z' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Resolved');
  });

  // History UI

  it('shows a loading state for history', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading history');

    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('shows an empty state when there is no history', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails, []);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No history yet.');
  });

  it('renders friendly text for status and assignment history entries', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails, [
      {
        id: 1,
        action: 'StatusChanged',
        previousValue: 'New',
        newValue: 'InProgress',
        previousDisplayValue: 'New',
        newDisplayValue: 'InProgress',
        changedBy: { id: 4, displayName: 'Support Agent' },
        createdAt: '2026-01-01T00:00:00Z',
      },
      {
        id: 2,
        action: 'AssignmentChanged',
        previousValue: null,
        newValue: '4',
        previousDisplayValue: null,
        newDisplayValue: 'Support Agent',
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-01T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Status changed from New to InProgress');
    expect(fixture.nativeElement.textContent).toContain('Assigned to Support Agent');
  });

  it('shows an error with retry when history fails to load', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load history. Please try again.');

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      'section[aria-labelledby="history-heading"] button',
    );
    retryButton.click();

    const req = httpMock.expectOne(`${requestsUrl}/42/history`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('reloads history after a successful assignment change', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__assignment-controls button').click();
    httpMock
      .expectOne(`${requestsUrl}/42/assignment`)
      .flush({ ...unassignedNewRequest, assignedTo: { id: 4, displayName: 'Support Agent' } });

    const req = httpMock.expectOne(`${requestsUrl}/42/history`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('reloads history after a successful status change', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__status-actions button').click();
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.confirm button:first-of-type').click();

    httpMock.expectOne(`${requestsUrl}/42/status`).flush({ ...unassignedNewRequest, status: 'Cancelled' });

    const req = httpMock.expectOne(`${requestsUrl}/42/history`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
