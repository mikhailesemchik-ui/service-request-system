import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { DASHBOARD_SUMMARY_PATH, DashboardSummary } from '../models/dashboard.models';

/** Talks to the `/api/dashboard` endpoint. Components must not build this URL themselves. */
@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}${DASHBOARD_SUMMARY_PATH}`;

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(this.url);
  }
}
