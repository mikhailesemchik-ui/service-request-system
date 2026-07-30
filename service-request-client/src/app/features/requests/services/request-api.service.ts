import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateRequestPayload,
  DEFAULT_PAGE_SIZE,
  PagedResult,
  REQUEST_ASSIGNEES_PATH,
  REQUESTS_PATH,
  RequestAssignee,
  RequestDetails,
  RequestHistoryItem,
  RequestListItem,
  RequestListQuery,
  UpdateRequestAssignmentPayload,
  UpdateRequestStatusPayload,
} from '../models/request.models';

/** Talks to the `/api/requests` endpoints. Components must not build these URLs themselves. */
@Injectable({ providedIn: 'root' })
export class RequestApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}${REQUESTS_PATH}`;

  getRequests(query: RequestListQuery = {}): Observable<PagedResult<RequestListItem>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? DEFAULT_PAGE_SIZE);

    if (query.status) {
      params = params.set('status', query.status);
    }

    if (query.priority) {
      params = params.set('priority', query.priority);
    }

    if (query.categoryId) {
      params = params.set('categoryId', query.categoryId);
    }

    return this.http.get<PagedResult<RequestListItem>>(this.baseUrl, { params });
  }

  getRequest(requestId: number): Observable<RequestDetails> {
    return this.http.get<RequestDetails>(`${this.baseUrl}/${requestId}`);
  }

  createRequest(payload: CreateRequestPayload): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(this.baseUrl, payload);
  }

  setAssignment(requestId: number, payload: UpdateRequestAssignmentPayload): Observable<RequestDetails> {
    return this.http.patch<RequestDetails>(`${this.baseUrl}/${requestId}/assignment`, payload);
  }

  changeStatus(requestId: number, payload: UpdateRequestStatusPayload): Observable<RequestDetails> {
    return this.http.patch<RequestDetails>(`${this.baseUrl}/${requestId}/status`, payload);
  }

  getHistory(requestId: number): Observable<RequestHistoryItem[]> {
    return this.http.get<RequestHistoryItem[]>(`${this.baseUrl}/${requestId}/history`);
  }

  getEligibleAssignees(): Observable<RequestAssignee[]> {
    return this.http.get<RequestAssignee[]>(`${environment.apiBaseUrl}${REQUEST_ASSIGNEES_PATH}`);
  }
}
