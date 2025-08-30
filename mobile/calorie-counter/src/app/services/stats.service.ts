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

export interface DayStats {
  date: string;
  totals: MacroTotals;
}

@Injectable({ providedIn: 'root' })
export class StatsService {
  constructor(private http: HttpClient, private auth: FoodBotAuthLinkService) {}
  private get baseUrl(): string { return this.auth.apiBaseUrl; }

  getSummary(days = 1): Observable<StatsSummary> {
    const params = new HttpParams().set('days', days);
    return this.http.get<StatsSummary>(`${this.baseUrl}/api/stats/summary`, { params });
  }

  getDaily(from: Date, to: Date): Observable<DayStats[]> {
    const params = new HttpParams()
      .set('from', this.formatDate(from))
      .set('to', this.formatDate(to));
    return this.http.get<DayStats[]>(`${this.baseUrl}/api/stats/daily`, { params });
  }

  private formatDate(d: Date): string {
    const shifted = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
    return shifted.toISOString().split('T')[0];
  }
}
