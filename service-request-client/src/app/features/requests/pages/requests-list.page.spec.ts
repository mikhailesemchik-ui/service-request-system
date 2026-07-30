import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Category } from '../../categories/models/category.models';
import { PagedResult, RequestListItem } from '../models/request.models';
import { RequestsListPageComponent } from './requests-list.page';

const categoriesUrl = `${environment.apiBaseUrl}/api/categories`;
const requestsUrl = `${environment.apiBaseUrl}/api/requests`;

const testCategory: Category = {
  id: 1,
  name: 'Hardware',
  description: null,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const testRequest: RequestListItem = {
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

function pagedResult(
  items: RequestListItem[],
  overrides: Partial<PagedResult<RequestListItem>> = {},
): PagedResult<RequestListItem> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...overrides };
}

describe('RequestsListPageComponent', () => {
  let fixture: ComponentFixture<RequestsListPageComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  function createFixture(role: UserRole = 'Employee', queryParams: Record<string, string> = {}): void {
    TestBed.configureTestingModule({
      imports: [RequestsListPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { hasAnyRole: (roles: UserRole[]) => roles.includes(role) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
      ],
    });

    fixture = TestBed.createComponent(RequestsListPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  }

  function flushCategories(categories: Category[] = [testCategory]): void {
    httpMock.expectOne((request) => request.url === categoriesUrl).flush(categories);
  }

  function flushRequests(result: PagedResult<RequestListItem> = pagedResult([testRequest])): void {
    httpMock.expectOne((request) => request.url === requestsUrl).flush(result);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a loading state before the initial request resolves', () => {
    createFixture();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.requests-page__status')?.textContent).toContain(
      'Loading requests',
    );

    flushCategories();
    flushRequests();
  });

  it('shows an empty state when there are no requests', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests(pagedResult([], { totalCount: 0, totalPages: 0 }));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No requests to show yet.');
  });

  it('shows an error state with a retry action when loading fails', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    httpMock.expectOne((request) => request.url === requestsUrl).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load requests. Please try again.');

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector('.requests-page__status button');
    retryButton.click();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.method).toBe('GET');
    req.flush(pagedResult([testRequest]));
  });

  it('shows a "My requests" heading for an Employee', () => {
    createFixture('Employee');
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('My requests');
  });

  it('shows an "All requests" heading for a SupportAgent', () => {
    createFixture('SupportAgent');
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('All requests');
  });

  it('shows an "All requests" heading for an Admin', () => {
    createFixture('Admin');
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('All requests');
  });

  it('renders a row for each returned request', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Printer not working');
  });

  it('shows an Unassigned fallback for requests with no assignee', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unassigned');
  });

  it('issues the correct API query when the status filter changes', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    const statusSelect: HTMLSelectElement = fixture.nativeElement.querySelector('#status-filter');
    statusSelect.value = 'New';
    statusSelect.dispatchEvent(new Event('change'));

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.get('status')).toBe('New');
    req.flush(pagedResult([testRequest]));
  });

  it('resets to page 1 when a filter changes', () => {
    createFixture('Admin', { page: '3' });
    fixture.detectChanges();
    flushCategories();
    flushRequests(pagedResult([testRequest], { page: 3, totalPages: 3 }));
    fixture.detectChanges();

    const prioritySelect: HTMLSelectElement = fixture.nativeElement.querySelector('#priority-filter');
    prioritySelect.value = 'High';
    prioritySelect.dispatchEvent(new Event('change'));

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.get('page')).toBe('1');
    req.flush(pagedResult([testRequest]));
  });

  it('requests the next page when Next is clicked', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests(pagedResult([testRequest], { totalPages: 3 }));
    fixture.detectChanges();

    const nextButton: HTMLButtonElement = fixture.nativeElement.querySelectorAll(
      '.requests-page__pagination button',
    )[1];
    nextButton.click();

    const req = httpMock.expectOne((request) => request.url === requestsUrl);
    expect(req.request.params.get('page')).toBe('2');
    req.flush(pagedResult([testRequest], { page: 2, totalPages: 3 }));
  });

  it('disables Previous on the first page and Next on the last page', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests(pagedResult([testRequest], { totalPages: 1 }));
    fixture.detectChanges();

    const buttons: HTMLButtonElement[] = fixture.nativeElement.querySelectorAll(
      '.requests-page__pagination button',
    );
    expect(buttons[0].disabled).toBeTrue();
    expect(buttons[1].disabled).toBeTrue();
  });

  it('navigates to request details when the row action is activated', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('table.request-table button').click();

    expect(router.navigate).toHaveBeenCalledWith(['/requests', testRequest.id]);
  });

  it('navigates to the create-request page', () => {
    createFixture();
    fixture.detectChanges();
    flushCategories();
    flushRequests();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.requests-page__header button').click();

    expect(router.navigate).toHaveBeenCalledWith(['/requests', 'new']);
  });
});
