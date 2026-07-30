import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserRole } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { environment } from '../../../../environments/environment';
import { Category } from '../models/category.models';
import { CategoriesPageComponent } from './categories.page';

const categoriesUrl = `${environment.apiBaseUrl}/api/categories`;

const activeCategory: Category = {
  id: 1,
  name: 'Hardware',
  description: 'Physical equipment issues',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-02-15T00:00:00Z',
};

const inactiveCategoryNoDescription: Category = {
  id: 2,
  name: 'Legacy',
  description: null,
  isActive: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-02-20T00:00:00Z',
};

describe('CategoriesPageComponent', () => {
  let fixture: ComponentFixture<CategoriesPageComponent>;
  let component: CategoriesPageComponent;
  let httpMock: HttpTestingController;

  function createFixture(role: UserRole = 'Employee'): void {
    TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { hasRole: (candidate: UserRole) => candidate === role } },
      ],
    });

    fixture = TestBed.createComponent(CategoriesPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  function expectInitialLoad(): ReturnType<HttpTestingController['expectOne']> {
    return httpMock.expectOne((request) => request.url === categoriesUrl);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a loading state before the initial request resolves', () => {
    createFixture();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.categories-page__status')?.textContent).toContain(
      'Loading categories',
    );

    expectInitialLoad().flush([]);
  });

  it('requests active categories only for an Employee', () => {
    createFixture('Employee');
    fixture.detectChanges();

    const req = expectInitialLoad();
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush([activeCategory]);
  });

  it('requests active categories only for a SupportAgent', () => {
    createFixture('SupportAgent');
    fixture.detectChanges();

    const req = expectInitialLoad();
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush([activeCategory]);
  });

  it('renders active categories after loading', () => {
    createFixture('Employee');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Hardware');
    expect(fixture.nativeElement.querySelector('.categories-page__status')).toBeNull();
  });

  it('renders an empty state when there are no categories', () => {
    createFixture('Employee');
    fixture.detectChanges();
    expectInitialLoad().flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No categories to show yet.');
  });

  it('renders an error state with a retry action when loading fails', () => {
    createFixture('Employee');
    fixture.detectChanges();
    expectInitialLoad().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load categories. Please try again.');
    expect(fixture.nativeElement.querySelector('.categories-page__status button')).not.toBeNull();
  });

  it('reloads the list when retry is clicked', () => {
    createFixture('Employee');
    fixture.detectChanges();
    expectInitialLoad().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector('.categories-page__status button');
    retryButton.click();

    const req = httpMock.expectOne((request) => request.url === categoriesUrl);
    expect(req.request.method).toBe('GET');
    req.flush([activeCategory]);
  });

  it('shows a read-only UI for an Employee (no create or management controls)', () => {
    createFixture('Employee');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.categories-page__header button')).toBeNull();
    expect(fixture.nativeElement.querySelector('.categories-page__include-inactive')).toBeNull();
    expect(fixture.nativeElement.querySelector('.category-table__actions')).toBeNull();
  });

  it('shows a read-only UI for a SupportAgent (no create or management controls)', () => {
    createFixture('SupportAgent');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.categories-page__header button')).toBeNull();
    expect(fixture.nativeElement.querySelector('.categories-page__include-inactive')).toBeNull();
  });

  it('shows create and management actions for an Admin', () => {
    createFixture('Admin');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory]);
    fixture.detectChanges();

    const createButton: HTMLButtonElement = fixture.nativeElement.querySelector('.categories-page__header button');
    expect(createButton.textContent).toContain('New category');
    expect(fixture.nativeElement.querySelector('.categories-page__include-inactive')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.category-table__actions')).not.toBeNull();
  });

  it('lets an Admin toggle include-inactive, reloading with the correct parameter', () => {
    createFixture('Admin');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory]);
    fixture.detectChanges();

    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '.categories-page__include-inactive input',
    );
    checkbox.click();

    const req = httpMock.expectOne((request) => request.url === categoriesUrl);
    expect(req.request.params.get('includeInactive')).toBe('true');
    req.flush([activeCategory, inactiveCategoryNoDescription]);
  });

  it('shows a description fallback and a visible status label', () => {
    createFixture('Admin');
    fixture.detectChanges();
    expectInitialLoad().flush([activeCategory, inactiveCategoryNoDescription]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No description');
    expect(fixture.nativeElement.textContent).toContain('Active');
    expect(fixture.nativeElement.textContent).toContain('Inactive');
  });

  it('renders category dates without crashing', () => {
    createFixture('Employee');
    expect(() => {
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();
    }).not.toThrow();
  });

  describe('active-state actions', () => {
    function rowActionButtons(): HTMLButtonElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('.category-table__actions button'));
    }

    it('asks for confirmation before deactivating and does not call the API until confirmed', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      rowActionButtons()[1].click();
      fixture.detectChanges();

      httpMock.expectNone(`${categoriesUrl}/${activeCategory.id}/active-state`);
      expect(fixture.nativeElement.querySelector('.confirm')).not.toBeNull();
    });

    it('does not call the API when deactivation confirmation is cancelled', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      rowActionButtons()[1].click();
      fixture.detectChanges();

      const cancelButton: HTMLButtonElement = fixture.nativeElement.querySelector('.confirm button:last-child');
      cancelButton.click();
      fixture.detectChanges();

      httpMock.expectNone(`${categoriesUrl}/${activeCategory.id}/active-state`);
      expect(fixture.nativeElement.querySelector('.confirm')).toBeNull();
    });

    it('deactivates an active category once confirmed, calling the API exactly once', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      rowActionButtons()[1].click();
      fixture.detectChanges();

      const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('.confirm button:first-of-type');
      confirmButton.click();

      const patchReq = httpMock.expectOne(`${categoriesUrl}/${activeCategory.id}/active-state`);
      expect(patchReq.request.method).toBe('PATCH');
      expect(patchReq.request.body).toEqual({ isActive: false });
      patchReq.flush({ ...activeCategory, isActive: false });

      expectInitialLoad().flush([{ ...activeCategory, isActive: false }]);
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('is now inactive');
    });

    it('reactivates an inactive category without requiring confirmation', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([inactiveCategoryNoDescription]);
      fixture.detectChanges();

      rowActionButtons()[1].click();

      const patchReq = httpMock.expectOne(`${categoriesUrl}/${inactiveCategoryNoDescription.id}/active-state`);
      expect(patchReq.request.body).toEqual({ isActive: true });
      patchReq.flush({ ...inactiveCategoryNoDescription, isActive: true });

      expectInitialLoad().flush([{ ...inactiveCategoryNoDescription, isActive: true }]);
    });

    it('disables the row action while the request is pending', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([inactiveCategoryNoDescription]);
      fixture.detectChanges();

      rowActionButtons()[1].click();
      fixture.detectChanges();

      expect(rowActionButtons()[1].disabled).toBeTrue();

      httpMock.expectOne(`${categoriesUrl}/${inactiveCategoryNoDescription.id}/active-state`).flush({
        ...inactiveCategoryNoDescription,
        isActive: true,
      });
      expectInitialLoad().flush([{ ...inactiveCategoryNoDescription, isActive: true }]);
    });

    it('shows a backend error message when the active-state change fails', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([inactiveCategoryNoDescription]);
      fixture.detectChanges();

      rowActionButtons()[1].click();

      httpMock
        .expectOne(`${categoriesUrl}/${inactiveCategoryNoDescription.id}/active-state`)
        .flush({ detail: 'Category not found' }, { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('Category not found');
    });

    it('gives a non-admin no visible way to trigger active-state or create/edit requests', () => {
      createFixture('Employee');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      expect(rowActionButtons().length).toBe(0);
      expect(fixture.nativeElement.querySelector('.categories-page__header button')).toBeNull();

      httpMock.expectNone(`${categoriesUrl}/${activeCategory.id}/active-state`);
      httpMock.expectNone((request) => request.method === 'POST' && request.url === categoriesUrl);
    });
  });

  describe('create and edit integration', () => {
    it('creates a category and refreshes the list', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      const createButton: HTMLButtonElement = fixture.nativeElement.querySelector('.categories-page__header button');
      createButton.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-category-form')).not.toBeNull();

      component['handleFormSave']({ name: 'Software', description: null });

      const postReq = httpMock.expectOne(categoriesUrl);
      expect(postReq.request.method).toBe('POST');
      postReq.flush({
        id: 3,
        name: 'Software',
        description: null,
        isActive: true,
        createdAt: '2026-03-01T00:00:00Z',
        updatedAt: '2026-03-01T00:00:00Z',
      });

      expectInitialLoad().flush([activeCategory, { ...activeCategory, id: 3, name: 'Software' }]);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-category-form')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('was created');
    });

    it('shows a duplicate-name conflict from the backend without closing the form', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      component['openCreateForm']();
      fixture.detectChanges();

      component['handleFormSave']({ name: 'Hardware', description: null });

      httpMock
        .expectOne(categoriesUrl)
        .flush({ detail: 'A category named "Hardware" already exists.' }, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-category-form')).not.toBeNull();
      expect(fixture.nativeElement.textContent).toContain('A category named "Hardware" already exists.');
    });

    it('opens the edit form prefilled and updates the category without changing isActive', () => {
      createFixture('Admin');
      fixture.detectChanges();
      expectInitialLoad().flush([activeCategory]);
      fixture.detectChanges();

      component['openEditForm'](activeCategory);
      fixture.detectChanges();

      expect(component['editingCategory']()).toEqual(activeCategory);

      component['handleFormSave']({ name: 'Hardware Support', description: activeCategory.description });

      const putReq = httpMock.expectOne(`${categoriesUrl}/${activeCategory.id}`);
      expect(putReq.request.method).toBe('PUT');
      expect(putReq.request.body).toEqual({ name: 'Hardware Support', description: activeCategory.description });
      putReq.flush({ ...activeCategory, name: 'Hardware Support' });

      expectInitialLoad().flush([{ ...activeCategory, name: 'Hardware Support' }]);
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('was updated');
    });
  });
});
