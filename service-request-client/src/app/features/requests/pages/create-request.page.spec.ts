import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Category } from '../../categories/models/category.models';
import { RequestDetails } from '../models/request.models';
import { CreateRequestPageComponent } from './create-request.page';

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

const testDetails: RequestDetails = {
  id: 42,
  title: 'Laptop does not start',
  description: 'The power button does not respond at all.',
  status: 'New',
  priority: 'High',
  category: { id: 1, name: 'Hardware' },
  createdBy: { id: 3, displayName: 'Development Employee' },
  assignedTo: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  resolvedAt: null,
  closedAt: null,
  cancelledAt: null,
};

describe('CreateRequestPageComponent', () => {
  let fixture: ComponentFixture<CreateRequestPageComponent>;
  let component: CreateRequestPageComponent;
  let httpMock: HttpTestingController;
  let router: Router;

  function createFixture(): void {
    TestBed.configureTestingModule({
      imports: [CreateRequestPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    fixture = TestBed.createComponent(CreateRequestPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  function flushCategories(categories: Category[] = [testCategory]): void {
    httpMock.expectOne((request) => request.url === categoriesUrl).flush(categories);
    fixture.detectChanges();
  }

  function fillValidForm(): void {
    component['form'].setValue({
      title: 'Laptop does not start',
      description: 'The power button does not respond at all.',
      categoryId: testCategory.id,
      priority: 'High',
    });
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('starts with an invalid form', () => {
    createFixture();
    flushCategories();

    expect(component['form'].invalid).toBeTrue();
  });

  it('marks title required and enforces its trimmed length', () => {
    createFixture();
    flushCategories();

    expect(component['form'].controls.title.hasError('required')).toBeTrue();

    component['form'].controls.title.setValue('ab');
    expect(component['form'].controls.title.errors?.['trimmedLength']).toBeTruthy();

    component['form'].controls.title.setValue('a'.repeat(201));
    expect(component['form'].controls.title.errors?.['trimmedLength']).toBeTruthy();
  });

  it('marks description required and enforces its trimmed length', () => {
    createFixture();
    flushCategories();

    expect(component['form'].controls.description.hasError('required')).toBeTrue();

    component['form'].controls.description.setValue('too short');
    expect(component['form'].controls.description.errors?.['trimmedLength']).toBeTruthy();
  });

  it('treats a whitespace-only title or description as invalid', () => {
    createFixture();
    flushCategories();

    component['form'].controls.title.setValue('    ');
    expect(component['form'].controls.title.hasError('required')).toBeTrue();

    component['form'].controls.description.setValue('          ');
    expect(component['form'].controls.description.hasError('required')).toBeTrue();
  });

  it('loads active categories only', () => {
    createFixture();

    const req = httpMock.expectOne((request) => request.url === categoriesUrl);
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush([testCategory]);
  });

  it('defaults priority to Medium', () => {
    createFixture();
    flushCategories();

    expect(component['form'].controls.priority.value).toBe('Medium');
  });

  it('does not submit until categories have loaded', () => {
    createFixture();
    fillValidForm();

    component['submit']();

    httpMock.expectNone(requestsUrl);
    expect(component['isSaving']()).toBeFalse();
    flushCategories();
  });

  it('normalizes whitespace in the submitted payload', () => {
    createFixture();
    flushCategories();

    component['form'].setValue({
      title: '  Laptop does not start  ',
      description: '  The power button does not respond at all.  ',
      categoryId: testCategory.id,
      priority: 'High',
    });
    component['submit']();

    const req = httpMock.expectOne(requestsUrl);
    expect(req.request.body).toEqual({
      title: 'Laptop does not start',
      description: 'The power button does not respond at all.',
      categoryId: testCategory.id,
      priority: 'High',
    });
    req.flush(testDetails);
  });

  it('prevents a duplicate submission while saving', () => {
    createFixture();
    flushCategories();
    fillValidForm();

    component['submit']();
    component['submit']();

    const req = httpMock.expectOne(requestsUrl);
    expect(req.request.method).toBe('POST');
    req.flush(testDetails);
  });

  it('preserves entered values when the server returns an error', () => {
    createFixture();
    flushCategories();
    fillValidForm();

    component['submit']();
    httpMock
      .expectOne(requestsUrl)
      .flush({ detail: 'The selected category is not active.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(component['form'].controls.title.value).toBe('Laptop does not start');
    expect(component['isSaving']()).toBeFalse();
    expect(component['serverError']()).toContain('no longer available');
  });

  it('navigates to the details page for the newly created request on success', () => {
    createFixture();
    flushCategories();
    fillValidForm();

    component['submit']();
    httpMock.expectOne(requestsUrl).flush(testDetails);

    expect(router.navigate).toHaveBeenCalledWith(['/requests', testDetails.id]);
  });

  it('navigates back to the list on cancel', () => {
    createFixture();
    flushCategories();

    component['cancel']();

    expect(router.navigate).toHaveBeenCalledWith(['/requests']);
  });
});
