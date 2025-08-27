import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FoodBotAuthLinkService } from './foodbot-auth-link.service';

export interface MacroTotals {
  calories: number;
  proteins: number;
  fats: number;
  carbs: number;
}

export interface StatsSummary {
  totals: MacroTotals;
  days: number;
  entries: number;
}

@Injectable({ providedIn: 'root' })
export class StatsService {
  constructor(private http: HttpClient, private auth: FoodBotAuthLinkService) {}
  private get baseUrl(): string { return this.auth.apiBaseUrl; }

  getSummary(days = 1): Observable<StatsSummary> {
    const params = new HttpParams().set('days', days);
    return this.http.get<StatsSummary>(`${this.baseUrl}/api/stats/summary`, { params });
  }
}
