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

  constructor(private http: HttpClient) {
    this.refresh();
  }

  refresh() {
    this.loading = true;
    this.http.get<AnalysisResponse>('/api/analysis').subscribe({
      next: r => {
        this.report = r;
        this.loading = false;
      },
      error: _ => {
        this.loading = false;
      }
    });
  }
}
