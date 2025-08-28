import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface AnalysisResponse {
  status: string;
  markdown?: string;
  createdAtUtc?: string;
}

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  report?: AnalysisResponse;
  loading = false;
  private timer?: any;

  constructor(private http: HttpClient) {}

  refresh() {
    if (this.loading) return;
    this.loading = true;
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = undefined;
    }
    this.http.get<AnalysisResponse>('/api/analysis').subscribe({
      next: r => {
        this.report = r;
        this.loading = false;
        if (r.status === 'processing') {
          this.timer = setTimeout(() => this.refresh(), 5000);
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
