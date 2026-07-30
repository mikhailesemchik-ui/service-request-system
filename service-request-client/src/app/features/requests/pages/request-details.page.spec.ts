import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { RequestDetails } from '../models/request.models';
import { RequestDetailsPageComponent } from './request-details.page';

const requestsUrl = `${environment.apiBaseUrl}/api/requests`;

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

describe('RequestDetailsPageComponent', () => {
  let fixture: ComponentFixture<RequestDetailsPageComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  function createFixture(requestId: string | null = '42'): void {
    TestBed.configureTestingModule({
      imports: [RequestDetailsPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
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

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a loading state before the request resolves', () => {
    createFixture();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading request');

    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
  });

  it('renders request data once loaded', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Laptop does not start');
    expect(fixture.nativeElement.textContent).toContain('Support Agent');
    expect(fixture.nativeElement.textContent).toContain('The power button does not respond at all.');
  });

  it('shows an Unassigned fallback when there is no assignee', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush({ ...testDetails, assignedTo: null });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unassigned');
  });

  it('renders lifecycle timestamps only when present', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Resolved');
    expect(fixture.nativeElement.textContent).not.toContain('Closed');
    expect(fixture.nativeElement.textContent).not.toContain('Cancelled');
  });

  it('shows an invalid-id message without calling the API for a non-numeric id', () => {
    createFixture('not-a-number');
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
  });

  it('navigates back to the requests list', () => {
    createFixture();
    fixture.detectChanges();
    httpMock.expectOne(`${requestsUrl}/42`).flush(testDetails);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.request-details-page__back').click();

    expect(router.navigate).toHaveBeenCalledWith(['/requests']);
  });
});
