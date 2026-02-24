import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { firstValueFrom, Observable, EMPTY, timer, throwError } from 'rxjs';
import { map, switchMap, take } from 'rxjs/operators';
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
  id?: number;
}

export interface PdfJobStatusResponse {
  status: 'processing' | 'ready' | 'error';
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
  create(period: AnalysisPeriod, options?: { date?: Date }): Promise<CreateReportResponse> {
    const payload: { period: AnalysisPeriod; date?: string } = { period };
    if (options?.date) {
      payload.date = this.formatDate(options.date);
    }

    return firstValueFrom(
      this.http.post<CreateReportResponse>(`${this.baseUrl}/api/analysis`, payload)
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

  /** Скачать Word-файл готового отчёта */
  downloadDocx(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/api/analysis/${id}/docx`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  /** Запросить создание PDF, возвращает идентификатор задания */
  createPdfJob(id: number): Observable<PdfJobResponse> {
    return this.http.post<{ id?: number; jobId?: string }>(`${this.baseUrl}/api/analysis/${id}/pdf`, {}).pipe(
      map(res => {
        const jobId = res.jobId ?? (res.id != null ? String(res.id) : '');
        if (!jobId) {
          throw new Error('Missing pdf job id in response');
        }
        return { id: res.id, jobId };
      })
    );
  }

  /** Опросить статус задания до готовности и скачать файл */
  pollPdfJob(jobId: string): Observable<HttpResponse<Blob>> {
    return timer(0, 1000).pipe(
      switchMap(() => this.http.get<PdfJobStatusResponse>(`${this.baseUrl}/api/analysis/pdf-jobs/${jobId}`)),
      switchMap(res => {
        if (res.status === 'processing') return EMPTY;
        if (res.status === 'error') {
          return throwError(() => new Error('Pdf job failed'));
        }

        return this.http.get(`${this.baseUrl}/api/analysis/pdf-jobs/${jobId}/file`, {
          responseType: 'blob',
          observe: 'response',
        });
      }),
      take(1)
    );
  }

  private formatDate(date: Date): string {
    const normalized = new Date(date);
    normalized.setHours(0, 0, 0, 0);
    const year = normalized.getFullYear();
    const month = (normalized.getMonth() + 1).toString().padStart(2, '0');
    const day = normalized.getDate().toString().padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
