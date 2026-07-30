import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CATEGORIES_PATH,
  Category,
  CreateCategoryRequest,
  UpdateCategoryActiveStateRequest,
  UpdateCategoryRequest,
} from '../models/category.models';

/** Talks to the `/api/categories` endpoints. Components must not build these URLs themselves. */
@Injectable({ providedIn: 'root' })
export class CategoryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}${CATEGORIES_PATH}`;

  getCategories(includeInactive = false): Observable<Category[]> {
    const params = new HttpParams().set('includeInactive', includeInactive);
    return this.http.get<Category[]>(this.baseUrl, { params });
  }

  getCategory(categoryId: number): Observable<Category> {
    return this.http.get<Category>(`${this.baseUrl}/${categoryId}`);
  }

  createCategory(request: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(this.baseUrl, request);
  }

  updateCategory(categoryId: number, request: UpdateCategoryRequest): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/${categoryId}`, request);
  }

  setActiveState(categoryId: number, isActive: boolean): Observable<Category> {
    const request: UpdateCategoryActiveStateRequest = { isActive };
    return this.http.patch<Category>(`${this.baseUrl}/${categoryId}/active-state`, request);
  }
}
