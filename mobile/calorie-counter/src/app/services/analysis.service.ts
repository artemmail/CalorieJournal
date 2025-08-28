import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FoodBotAuthLinkService } from './foodbot-auth-link.service';

export interface AnalysisResponse {
  status: string;
  markdown?: string;
  createdAtUtc?: string;
}

export type AnalysisPeriod = 'day' | 'week' | 'month' | 'quarter';

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  report?: AnalysisResponse;
  loading = false;
  private timer?: any;

  constructor(private http: HttpClient, private auth: FoodBotAuthLinkService) {}

  private get baseUrl(): string { return this.auth.apiBaseUrl; }

  refresh(period: AnalysisPeriod = 'day') {
    if (this.loading) return;
    this.loading = true;
    this.report = undefined;
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = undefined;
    }
    this.http.get<AnalysisResponse>(`${this.baseUrl}/api/analysis?period=${period}`).subscribe({
      next: r => {
        this.report = r;
        this.loading = false;
        if (period === 'day' && r.status === 'processing') {
          this.timer = setTimeout(() => this.refresh(period), 5000);
        }
      },
      error: _ => {
        this.loading = false;
      }
    });
  }

  cancel() {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = undefined;
    }
  }
}
