import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { Category } from '../models/category.models';
import { CategoryApiService } from './category-api.service';

const categoriesUrl = `${environment.apiBaseUrl}/api/categories`;

const testCategory: Category = {
  id: 1,
  name: 'Hardware',
  description: 'Physical equipment issues',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('CategoryApiService', () => {
  let service: CategoryApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(CategoryApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads active categories by default', () => {
    service.getCategories().subscribe();

    const req = httpMock.expectOne((request) => request.url === categoriesUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush([testCategory]);
  });

  it('passes includeInactive=true when requested', () => {
    service.getCategories(true).subscribe();

    const req = httpMock.expectOne((request) => request.url === categoriesUrl);
    expect(req.request.params.get('includeInactive')).toBe('true');
    req.flush([testCategory]);
  });

  it('retrieves one category by id', () => {
    service.getCategory(1).subscribe((category) => {
      expect(category).toEqual(testCategory);
    });

    const req = httpMock.expectOne(`${categoriesUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(testCategory);
  });

  it('creates a category with the correct body', () => {
    service.createCategory({ name: 'Software', description: null }).subscribe();

    const req = httpMock.expectOne(categoriesUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Software', description: null });
    req.flush(testCategory);
  });

  it('updates a category with the correct body', () => {
    service.updateCategory(1, { name: 'Hardware', description: 'Updated' }).subscribe();

    const req = httpMock.expectOne(`${categoriesUrl}/1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Hardware', description: 'Updated' });
    req.flush(testCategory);
  });

  it('changes active state with the correct PATCH body', () => {
    service.setActiveState(1, false).subscribe();

    const req = httpMock.expectOne(`${categoriesUrl}/1/active-state`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush({ ...testCategory, isActive: false });
  });
});
