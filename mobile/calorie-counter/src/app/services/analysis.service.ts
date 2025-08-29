import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FoodBotAuthLinkService } from './foodbot-auth-link.service';

export type AnalysisPeriod = 'day' | 'week' | 'month' | 'quarter';

export interface ReportRow {
  id: number;
  name: string;
  period: AnalysisPeriod;
  createdAtUtc: string;
  isProcessing: boolean;
  checksum: number;
  hasMarkdown: boolean;
}

export interface CreateReportResponse {
  status: 'processing' | 'ok' | 'no_changes';
  id: number;
  name: string;
  period: AnalysisPeriod;
}

export interface GetReportResponse {
  status: 'ok' | 'processing';
  markdown?: string;
  createdAtUtc?: string;
  checksum?: number;
  name?: string;
  period?: AnalysisPeriod;
}

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  constructor(private http: HttpClient, private auth: FoodBotAuthLinkService) {}

  private get baseUrl(): string { return this.auth.apiBaseUrl; }

  /** История сохранённых отчётов */
  list(): Promise<ReportRow[]> {
    return firstValueFrom(
      this.http.get<ReportRow[]>(`${this.baseUrl}/api/analysis/reports`)
    );
  }

  /** Создать новый отчёт указанного периода */
  create(period: AnalysisPeriod): Promise<CreateReportResponse> {
    return firstValueFrom(
      this.http.post<CreateReportResponse>(`${this.baseUrl}/api/analysis`, { period })
    );
  }

  /** Получить сохранённый отчёт по id */
  getById(id: number): Promise<GetReportResponse> {
    return firstValueFrom(
      this.http.get<GetReportResponse>(`${this.baseUrl}/api/analysis/${id}`)
    );
  }
}
