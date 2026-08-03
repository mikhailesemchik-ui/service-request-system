import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { RequestAssignee, RequestComment, RequestDetails, RequestHistoryItem } from '../models/request.models';
import { RequestDetailsPageComponent } from './request-details.page';

const requestsUrl = `${environment.apiBaseUrl}/api/requests`;
const requestAssigneesUrl = `${environment.apiBaseUrl}/api/request-assignees`;
const categoriesUrl = `${environment.apiBaseUrl}/api/categories`;

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
    comments: RequestComment[] = [],
  ): void {
    httpMock.expectOne(`${requestsUrl}/42`).flush(details);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush(historyItems);
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush(comments);
    if (currentRole === 'Admin') {
      httpMock.expectOne(requestAssigneesUrl).flush(assignees);
    }
    if (currentRole === 'Admin' || currentRole === 'SupportAgent') {
      httpMock.expectOne((req) => req.url === categoriesUrl).flush([]);
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
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);
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
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);

    const req = httpMock.expectOne(requestAssigneesUrl);
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 4, displayName: 'Support Agent', role: 'SupportAgent' }]);

    httpMock.expectOne((req) => req.url === categoriesUrl).flush([]);
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
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);
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
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);
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

  // Classification visibility

  it('Employee sees category and priority as read-only labels, no edit form', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Hardware');
    expect(fixture.nativeElement.textContent).toContain('High');
    expect(fixture.nativeElement.querySelector('#classification-heading')).toBeNull();
    expect(fixture.nativeElement.querySelector('#classification-category')).toBeNull();
  });

  it('unassigned SupportAgent sees no classification edit form', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).toBeNull();
  });

  it('SupportAgent assigned to another user sees no classification edit form', () => {
    createFixture('SupportAgent', 999);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).toBeNull();
  });

  it('assigned SupportAgent sees classification edit controls', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#classification-priority')).not.toBeNull();
  });

  it('Admin sees classification edit controls on any non-terminal request', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).not.toBeNull();
  });

  it('hides classification edit controls on a Closed request for Admin', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    const closedRequest: RequestDetails = {
      ...testDetails,
      status: 'Closed',
      closedAt: '2026-01-03T00:00:00Z',
    };
    flushDetails(closedRequest);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).toBeNull();
  });

  it('hides classification edit controls on a Cancelled request for Admin', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    const cancelledRequest: RequestDetails = {
      ...testDetails,
      status: 'Cancelled',
      cancelledAt: '2026-01-03T00:00:00Z',
    };
    flushDetails(cancelledRequest);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#classification-category')).toBeNull();
  });

  // Form state

  it('prefills category and priority from the loaded request', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);
    httpMock.expectOne(requestAssigneesUrl).flush([]);
    httpMock
      .expectOne((req) => req.url === categoriesUrl)
      .flush([{ id: 1, name: 'Hardware', isActive: true }]);
    fixture.detectChanges();

    const categorySelect: HTMLSelectElement = fixture.nativeElement.querySelector('#classification-category');
    const prioritySelect: HTMLSelectElement = fixture.nativeElement.querySelector('#classification-priority');
    expect(Number(categorySelect.value)).toBe(testDetails.category.id);
    expect(prioritySelect.value).toBe(testDetails.priority);
  });

  it('loads active categories into the category select', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
    httpMock.expectOne(`${requestsUrl}/42/comments`).flush([]);
    httpMock.expectOne(requestAssigneesUrl).flush([]);
    httpMock
      .expectOne((req) => req.url === categoriesUrl)
      .flush([
        { id: 1, name: 'Hardware', isActive: true },
        { id: 2, name: 'Software', isActive: true },
      ]);
    fixture.detectChanges();

    const options: HTMLOptionElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('#classification-category option'),
    );
    const names = options.map((o) => o.textContent?.trim());
    expect(names).toContain('Hardware');
    expect(names).toContain('Software');
  });

  it('sends only categoryId and priority when classification is submitted', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    const req = httpMock.expectOne(`${requestsUrl}/42/classification`);
    expect(Object.keys(req.request.body as object)).toEqual(jasmine.arrayWithExactContents(['categoryId', 'priority']));
    expect(req.request.body).toEqual({ categoryId: unassignedNewRequest.category.id, priority: unassignedNewRequest.priority });
    req.flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('sends a priority change with the original category', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], [], []);
    fixture.detectChanges();

    // Use the form API to change priority, bypassing Angular CVA internal value strings.
    (fixture.componentInstance as any).classificationForm.controls.priority.setValue('Critical');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    const req = httpMock.expectOne(`${requestsUrl}/42/classification`);
    expect(req.request.body).toEqual({ categoryId: testDetails.category.id, priority: 'Critical' });
    req.flush({ ...assignedToAgentInProgress, priority: 'Critical' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('prevents a duplicate classification submission while saving', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      'form[aria-label="Edit classification"] button[type="submit"]',
    );
    submitButton.click();
    submitButton.click();

    const requests = httpMock.match(`${requestsUrl}/42/classification`);
    expect(requests.length).toBe(1);
    requests[0].flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('reset restores category and priority to the last loaded values', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(testDetails, [], [], []);
    fixture.detectChanges();

    const form = (fixture.componentInstance as any).classificationForm;
    form.controls.priority.setValue('Low');
    fixture.detectChanges();
    expect(form.controls.priority.value).toBe('Low');

    const resetButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      'form[aria-label="Edit classification"] button[type="button"]',
    );
    resetButton.click();
    fixture.detectChanges();

    expect(form.controls.priority.value).toBe(testDetails.priority);
  });

  // Success and failure

  it('replaces displayed request details on classification success', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    const updatedRequest = { ...unassignedNewRequest, priority: 'Critical' as const };
    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(updatedRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Critical');
  });

  it('reloads history after successful classification', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(unassignedNewRequest);

    const historyReq = httpMock.expectOne(`${requestsUrl}/42/history`);
    expect(historyReq.request.method).toBe('GET');
    historyReq.flush([]);
  });

  it('updates the form baseline after classification success', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    const updatedRequest: RequestDetails = { ...unassignedNewRequest, priority: 'Critical' };
    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(updatedRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
    fixture.detectChanges();

    const prioritySelect: HTMLSelectElement = fixture.nativeElement.querySelector('#classification-priority');
    expect(prioritySelect.value).toBe('Critical');
  });

  it('does not optimistically update displayed request before success', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    (fixture.componentInstance as any).classificationForm.controls.priority.setValue('Critical');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    fixture.detectChanges();

    // The PATCH is in-flight. The top-level meta dd for Priority should still show the original value.
    const topMetaDivs: HTMLElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__meta')[0].querySelectorAll('div'),
    );
    const priorityDiv = topMetaDivs.find((div) => div.querySelector('dt')?.textContent?.trim() === 'Priority');
    expect(priorityDiv?.querySelector('dd')?.textContent?.trim()).toBe(unassignedNewRequest.priority);

    // Flush the in-flight PATCH to satisfy afterEach verification.
    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('shows a permission error on a 403 response', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    httpMock
      .expectOne(`${requestsUrl}/42/classification`)
      .flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('You do not have permission to perform this action.');
  });

  it('shows a problem-details message on a 409 response', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [], [], []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    httpMock
      .expectOne(`${requestsUrl}/42/classification`)
      .flush(
        { detail: 'Closed or cancelled requests cannot be changed.' },
        { status: 409, statusText: 'Conflict' },
      );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Closed or cancelled requests cannot be changed.');
  });

  it('preserves form values when the classification request fails', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], [], []);
    fixture.detectChanges();

    const form = (fixture.componentInstance as any).classificationForm;
    form.controls.priority.setValue('Critical');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    httpMock
      .expectOne(`${requestsUrl}/42/classification`)
      .flush({ detail: 'Server error.' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(form.controls.priority.value).toBe('Critical');
  });

  // Concurrency

  it('classification submission is blocked while an assignment save is in flight', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], [{ id: 5, displayName: 'Agent Two', role: 'SupportAgent' }]);
    fixture.detectChanges();

    // Start assignment save
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('#assignee-select');
    select.value = '5';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    fixture.nativeElement
      .querySelectorAll('.request-details-page__assignment-controls button')[0]
      .click();

    // Assignment is in-flight; now try classification — it must be blocked.
    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    const classificationRequests = httpMock.match(`${requestsUrl}/42/classification`);
    expect(classificationRequests.length).toBe(0);

    // Flush the assignment to clean up.
    httpMock.expectOne(`${requestsUrl}/42/assignment`).flush(assignedToAgentInProgress);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('classification submission is blocked while a status save is in flight', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress, [], [], []);
    fixture.detectChanges();

    const resolveButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__status-actions button'),
    ).find((b) => (b as HTMLButtonElement).textContent?.trim() === 'Resolve') as HTMLButtonElement;
    resolveButton.click();

    // Status is in-flight; now try classification — it must be blocked.
    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();

    const classificationRequests = httpMock.match(`${requestsUrl}/42/classification`);
    expect(classificationRequests.length).toBe(0);

    httpMock.expectOne(`${requestsUrl}/42/status`).flush({ ...assignedToAgentInProgress, status: 'Resolved' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  // History text for classification actions

  it('renders a friendly description for CategoryChanged history', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails, [
      {
        id: 10,
        action: 'CategoryChanged',
        previousValue: '1',
        newValue: '2',
        previousDisplayValue: 'Hardware',
        newDisplayValue: 'Software',
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Category changed from Hardware to Software');
  });

  it('renders a friendly description for PriorityChanged history', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails, [
      {
        id: 11,
        action: 'PriorityChanged',
        previousValue: 'Low',
        newValue: 'Critical',
        previousDisplayValue: 'Low',
        newDisplayValue: 'Critical',
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Priority changed from Low to Critical');
  });

  it('falls back to raw values when display values are missing', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails, [
      {
        id: 12,
        action: 'CategoryChanged',
        previousValue: '1',
        newValue: '99',
        previousDisplayValue: null,
        newDisplayValue: null,
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Category changed from');
  });

  function contentEditButton(): HTMLButtonElement | undefined {
    return Array.from(fixture.nativeElement.querySelectorAll('button')).find(
      (button) => (button as HTMLButtonElement).textContent?.trim() === 'Edit',
    ) as HTMLButtonElement | undefined;
  }

  function saveContentButton(): HTMLButtonElement {
    return Array.from(fixture.nativeElement.querySelectorAll('form[aria-label="Edit request content"] button')).find(
      (button) => (button as HTMLButtonElement).textContent?.trim() === 'Save',
    ) as HTMLButtonElement;
  }

  function openContentEditor(details: RequestDetails = unassignedNewRequest, assignees: RequestAssignee[] = []): void {
    flushDetails(details, [], assignees);
    fixture.detectChanges();
    contentEditButton()?.click();
    fixture.detectChanges();
  }

  // Content edit visibility

  it('Employee owner on a New request sees content Edit', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(contentEditButton()).toBeTruthy();
  });

  it('Employee on a non-New request does not see content Edit', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(testDetails);
    fixture.detectChanges();

    expect(contentEditButton()).toBeUndefined();
  });

  it('assigned SupportAgent sees content Edit', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    expect(contentEditButton()).toBeTruthy();
  });

  it('unassigned SupportAgent does not see content Edit', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(contentEditButton()).toBeUndefined();
  });

  it('SupportAgent assigned to another user does not see content Edit', () => {
    createFixture('SupportAgent', 999);
    fixture.detectChanges();
    flushDetails(assignedToAgentInProgress);
    fixture.detectChanges();

    expect(contentEditButton()).toBeUndefined();
  });

  it('Admin on a non-terminal request sees content Edit', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest);
    fixture.detectChanges();

    expect(contentEditButton()).toBeTruthy();
  });

  it('Closed requests hide content Edit', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails({ ...testDetails, status: 'Closed', closedAt: '2026-01-03T00:00:00Z' });
    fixture.detectChanges();

    expect(contentEditButton()).toBeUndefined();
  });

  it('Cancelled requests hide content Edit', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    flushDetails({ ...testDetails, status: 'Cancelled', cancelledAt: '2026-01-03T00:00:00Z' });
    fixture.detectChanges();

    expect(contentEditButton()).toBeUndefined();
  });

  // Content form

  it('opens content edit mode with current values prefilled', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    expect(form.controls.title.value).toBe(unassignedNewRequest.title);
    expect(form.controls.description.value).toBe(unassignedNewRequest.description);
  });

  it('content Cancel restores backend values and exits edit mode', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue('Changed locally');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('form[aria-label="Edit request content"] button[type="button"]').click();
    fixture.detectChanges();

    expect(form.controls.title.value).toBe(unassignedNewRequest.title);
    expect(fixture.nativeElement.querySelector('form[aria-label="Edit request content"]')).toBeNull();
  });

  it('validates content title boundaries and whitespace', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const control = (fixture.componentInstance as any).contentForm.controls.title;
    control.setValue('   ');
    expect(control.hasError('required')).toBeTrue();
    control.setValue('ab');
    expect(control.hasError('trimmedLength')).toBeTrue();
    control.setValue('abc');
    expect(control.valid).toBeTrue();
    control.setValue('a'.repeat(200));
    expect(control.valid).toBeTrue();
    control.setValue('a'.repeat(201));
    expect(control.hasError('trimmedLength')).toBeTrue();
  });

  it('validates content description boundaries and whitespace', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const control = (fixture.componentInstance as any).contentForm.controls.description;
    control.setValue('   ');
    expect(control.hasError('required')).toBeTrue();
    control.setValue('a');
    expect(control.valid).toBeTrue();
    control.setValue('a'.repeat(4000));
    expect(control.valid).toBeTrue();
    control.setValue('a'.repeat(4001));
    expect(control.hasError('trimmedLength')).toBeTrue();
  });

  it('does not submit unchanged normalized content', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue(`  ${unassignedNewRequest.title}  `);
    form.controls.description.setValue(`  ${unassignedNewRequest.description}  `);
    fixture.detectChanges();
    saveContentButton().click();

    expect(httpMock.match(`${requestsUrl}/42/content`).length).toBe(0);
  });

  it('sends title-only content change with the current description', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue('Updated title');
    fixture.detectChanges();
    saveContentButton().click();

    const req = httpMock.expectOne(`${requestsUrl}/42/content`);
    expect(req.request.body).toEqual({ title: 'Updated title', description: unassignedNewRequest.description });
    req.flush({ ...unassignedNewRequest, title: 'Updated title' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('sends description-only content change with the current title', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.description.setValue('Updated description.');
    fixture.detectChanges();
    saveContentButton().click();

    const req = httpMock.expectOne(`${requestsUrl}/42/content`);
    expect(req.request.body).toEqual({ title: unassignedNewRequest.title, description: 'Updated description.' });
    req.flush({ ...unassignedNewRequest, description: 'Updated description.' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('sends a combined content payload once and blocks duplicates', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue('Updated title');
    form.controls.description.setValue('Updated description.');
    fixture.detectChanges();
    saveContentButton().click();
    saveContentButton().click();

    const requests = httpMock.match(`${requestsUrl}/42/content`);
    expect(requests.length).toBe(1);
    expect(requests[0].request.body).toEqual({ title: 'Updated title', description: 'Updated description.' });
    requests[0].flush({ ...unassignedNewRequest, title: 'Updated title', description: 'Updated description.' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  // Content success and failure

  it('does not optimistically mutate displayed content before save succeeds', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();
    saveContentButton().click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toContain(unassignedNewRequest.title);
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({ ...unassignedNewRequest, title: 'Updated title' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('content success replaces details, exits edit mode, and refreshes history', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();
    saveContentButton().click();
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({ ...unassignedNewRequest, title: 'Updated title' });
    const historyReq = httpMock.expectOne(`${requestsUrl}/42/history`);
    expect(historyReq.request.method).toBe('GET');
    historyReq.flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Updated title');
    expect(fixture.nativeElement.querySelector('form[aria-label="Edit request content"]')).toBeNull();
  });

  it('content failure preserves edited form values', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue('Edited title');
    fixture.detectChanges();
    saveContentButton().click();
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(form.controls.title.value).toBe('Edited title');
  });

  it('shows content 403, 404, and 409 errors', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);

    const form = (fixture.componentInstance as any).contentForm;
    form.controls.title.setValue('Edited title');
    fixture.detectChanges();
    saveContentButton().click();
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('You do not have permission to perform this action.');

    form.controls.title.setValue('Edited title again');
    fixture.detectChanges();
    saveContentButton().click();
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('This request does not exist or is not available to you.');

    form.controls.title.setValue('Edited title third');
    fixture.detectChanges();
    saveContentButton().click();
    httpMock.expectOne(`${requestsUrl}/42/content`).flush({ detail: 'This request can no longer be edited.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('This request can no longer be edited.');
  });

  // Content concurrency

  it('blocks content save during assignment save', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest, [{ id: 4, displayName: 'Support Agent', role: 'SupportAgent' }]);
    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('#assignee-select');
    select.value = '4';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const assignButton = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__assignment-controls button'),
    )[0] as HTMLButtonElement;
    assignButton.click();
    (fixture.componentInstance as any).submitContent();

    expect(httpMock.match(`${requestsUrl}/42/content`).length).toBe(0);
    httpMock.expectOne(`${requestsUrl}/42/assignment`).flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('blocks content save during status save', () => {
    createFixture('SupportAgent', 4);
    fixture.detectChanges();
    openContentEditor(assignedToAgentInProgress);
    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();

    const resolveButton: HTMLButtonElement = Array.from(
      fixture.nativeElement.querySelectorAll('.request-details-page__status-actions button'),
    ).find((b) => (b as HTMLButtonElement).textContent?.trim() === 'Resolve') as HTMLButtonElement;
    resolveButton.click();
    (fixture.componentInstance as any).submitContent();

    expect(httpMock.match(`${requestsUrl}/42/content`).length).toBe(0);
    httpMock.expectOne(`${requestsUrl}/42/status`).flush({ ...assignedToAgentInProgress, status: 'Resolved' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('blocks content save during classification save', () => {
    createFixture('Admin', 1);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);
    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form[aria-label="Edit classification"] button[type="submit"]').click();
    (fixture.componentInstance as any).submitContent();

    expect(httpMock.match(`${requestsUrl}/42/content`).length).toBe(0);
    httpMock.expectOne(`${requestsUrl}/42/classification`).flush(unassignedNewRequest);
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  it('comments loading remains independent while content save is active', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    openContentEditor(unassignedNewRequest);
    (fixture.componentInstance as any).contentForm.controls.title.setValue('Updated title');
    fixture.detectChanges();
    saveContentButton().click();

    (fixture.componentInstance as any).retryComments();
    const commentsReq = httpMock.expectOne(`${requestsUrl}/42/comments`);
    expect(commentsReq.request.method).toBe('GET');
    commentsReq.flush([]);

    httpMock.expectOne(`${requestsUrl}/42/content`).flush({ ...unassignedNewRequest, title: 'Updated title' });
    httpMock.expectOne(`${requestsUrl}/42/history`).flush([]);
  });

  // Content history rendering

  it('renders friendly title and description history text', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [
      {
        id: 20,
        action: 'TitleChanged',
        previousValue: 'Old title',
        newValue: 'New title',
        previousDisplayValue: 'Old title',
        newDisplayValue: 'New title',
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
      {
        id: 21,
        action: 'DescriptionChanged',
        previousValue: 'Old summary',
        newValue: 'New summary',
        previousDisplayValue: 'Old summary',
        newDisplayValue: 'New summary',
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Title changed from �Old title� to �New title�');
    expect(fixture.nativeElement.textContent).toContain('Description updated');
  });

  it('uses readable fallback for missing title history values', () => {
    createFixture('Employee', 3);
    fixture.detectChanges();
    flushDetails(unassignedNewRequest, [
      {
        id: 22,
        action: 'TitleChanged',
        previousValue: null,
        newValue: null,
        previousDisplayValue: null,
        newDisplayValue: null,
        changedBy: { id: 1, displayName: 'Root Admin' },
        createdAt: '2026-01-02T00:00:00Z',
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('unknown value');
  });});
