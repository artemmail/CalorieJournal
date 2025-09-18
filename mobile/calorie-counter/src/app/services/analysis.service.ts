import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { firstValueFrom, Observable, EMPTY, timer } from 'rxjs';
import { filter, switchMap, take } from 'rxjs/operators';
import { FoodBotAuthLinkService } from './foodbot-auth-link.service';

export type AnalysisPeriod = 'day' | 'dayRemainder' | 'week' | 'month' | 'quarter';

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

export interface PdfJobResponse {
  jobId: string;
}

export interface PdfJobStatusResponse {
  status: 'processing' | 'ready';
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

  /** Запустить генерацию PDF и скачать, когда будет готово */
  downloadPdf(id: number): Observable<HttpResponse<Blob>> {
    return this.createPdfJob(id).pipe(
      switchMap(res => this.pollPdfJob(res.jobId))
    );
  }

  /** Запросить создание PDF, возвращает идентификатор задания */
  createPdfJob(id: number): Observable<PdfJobResponse> {
    return this.http.post<PdfJobResponse>(`${this.baseUrl}/api/analysis/${id}/pdf`, {});
  }

  /** Опросить статус задания до готовности и скачать файл */
  pollPdfJob(jobId: string): Observable<HttpResponse<Blob>> {
    return timer(0, 1000).pipe(
      switchMap(() => this.http.get<PdfJobStatusResponse>(`${this.baseUrl}/api/analysis/pdf-jobs/${jobId}`)),
      switchMap(res => {
        if (res.status !== 'ready') return EMPTY;
        return this.http.get(`${this.baseUrl}/api/analysis/pdf-jobs/${jobId}`, {
          responseType: 'blob',
          observe: 'response',
        });
      }),
      filter(x => !!x),
      take(1)
    );
  }
}
