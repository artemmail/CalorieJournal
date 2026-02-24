import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { InfiniteScrollModule } from 'ngx-infinite-scroll';
import { AnalysisService, AnalysisPeriod, ReportRow } from '../../services/analysis.service';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AnalysisDateDialogComponent } from './analysis-date-dialog.component';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { PersonalCard } from '../../services/foodbot-api.types';
import { ProfileRequiredDialogComponent } from './profile-required-dialog.component';

type FilterPeriod = AnalysisPeriod | 'all';
type FilterStatus = 'all' | 'processing' | 'ready' | 'error';
type ReportStatus = Exclude<FilterStatus, 'all'>;

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    MatSnackBarModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
    InfiniteScrollModule
  ],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage implements OnInit, OnDestroy {
  cols = ['date', 'name', 'period', 'status', 'actions'];

  readonly periodFilters: Array<{ value: FilterPeriod; label: string }> = [
    { value: 'all', label: 'Все периоды' },
    { value: 'day', label: 'День · итог' },
    { value: 'dayRemainder', label: 'День · остаток' },
    { value: 'week', label: 'Неделя' },
    { value: 'month', label: 'Месяц' },
    { value: 'quarter', label: 'Квартал' }
  ];

  readonly statusFilters: Array<{ value: FilterStatus; label: string }> = [
    { value: 'all', label: 'Все статусы' },
    { value: 'processing', label: 'В обработке' },
    { value: 'ready', label: 'Готово' },
    { value: 'error', label: 'Ошибка' }
  ];

  readonly skeletonRows = Array.from({ length: 4 });

  searchTerm = '';
  selectedPeriod: FilterPeriod = 'all';
  selectedStatus: FilterStatus = 'all';

  rows: ReportRow[] = [];
  filteredRows: ReportRow[] = [];
  allRows: ReportRow[] = [];
  pageSize = 12;
  loading = false;
  loadError = '';
  hasProcessing = false;
  isFirstLoad = true;

  private timer?: any;

  constructor(
    private api: AnalysisService,
    private sb: MatSnackBar,
    private router: Router,
    private dialog: MatDialog,
    private foodbotApi: FoodbotApiService
  ) {}

  ngOnInit(): void {
    this.refresh();
    this.timer = setInterval(() => {
      if (this.hasProcessing) {
        this.refresh(false);
      }
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  async refresh(showSpinner = true) {
    try {
      if (showSpinner) {
        this.loading = true;
      }
      this.loadError = '';
      const list = await this.api.list();
      this.allRows = list;
      this.hasProcessing = list.some(x => x.isProcessing);
      this.applyFilters();
    } catch {
      this.loadError = 'Не удалось загрузить историю отчётов.';
      this.sb.open('Не удалось загрузить историю отчётов.', 'Закрыть', { duration: 3500 });
    } finally {
      this.loading = false;
      this.isFirstLoad = false;
    }
  }

  onFiltersChanged() {
    this.applyFilters();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedPeriod = 'all';
    this.selectedStatus = 'all';
    this.applyFilters();
  }

  hasActiveFilters(): boolean {
    return this.searchTerm.trim().length > 0 || this.selectedPeriod !== 'all' || this.selectedStatus !== 'all';
  }

  loadMore(reset = false) {
    if (reset) {
      this.rows = [];
    }
    const next = this.filteredRows.slice(this.rows.length, this.rows.length + this.pageSize);
    if (next.length) {
      this.rows = [...this.rows, ...next];
    }
  }

  hasMoreRows(): boolean {
    return this.rows.length < this.filteredRows.length;
  }

  onScrollDown() {
    if (this.loading || !this.hasMoreRows()) {
      return;
    }
    this.loadMore();
  }

  async create(period: AnalysisPeriod, date?: Date, skipProfileCheck = false) {
    if (!skipProfileCheck && !(await this.ensureProfileFilled())) {
      return;
    }
    try {
      this.loading = true;
      const res = await this.api.create(period, date ? { date } : undefined);
      if (res.status === 'no_changes') {
        this.sb.open('Новых приёмов пищи не было — отчёт не пересчитывался.', 'OK', { duration: 4000 });
      } else {
        this.sb.open('Отчёт поставлен в очередь. Статус обновится автоматически.', 'OK', { duration: 3000 });
      }
      await this.refresh(false);
    } catch {
      this.sb.open('Не удалось создать отчёт.', 'Закрыть', { duration: 4000 });
    } finally {
      this.loading = false;
    }
  }

  repeat(row: ReportRow) {
    if (row.isProcessing) {
      return;
    }
    this.create(row.period);
  }

  async createFromDate() {
    if (!(await this.ensureProfileFilled())) {
      return;
    }
    const ref = this.dialog.open(AnalysisDateDialogComponent);
    const selected = await firstValueFrom(ref.afterClosed());
    if (!selected) return;
    await this.create(selected.period, selected.date, true);
  }

  open(row: ReportRow) {
    this.router.navigate(['/analysis', row.id]);
  }

  reportStatus(row: ReportRow): ReportStatus {
    if (row.isProcessing) {
      return 'processing';
    }
    return row.hasMarkdown ? 'ready' : 'error';
  }

  reportStatusLabel(row: ReportRow): string {
    const status = this.reportStatus(row);
    if (status === 'processing') {
      return 'В обработке';
    }
    if (status === 'ready') {
      return 'Готово';
    }
    return 'Ошибка';
  }

  trackByReportId(_: number, row: ReportRow): number {
    return row.id;
  }

  periodLabel(p: AnalysisPeriod) {
    switch (p) {
      case 'day': return 'День · итог';
      case 'dayRemainder': return 'День · остаток';
      case 'week': return 'Неделя';
      case 'month': return 'Месяц';
      case 'quarter': return 'Квартал';
    }
  }

  private applyFilters() {
    const term = this.searchTerm.trim().toLowerCase();

    this.filteredRows = this.allRows.filter(row => {
      if (term && !row.name.toLowerCase().includes(term)) {
        return false;
      }
      if (this.selectedPeriod !== 'all' && row.period !== this.selectedPeriod) {
        return false;
      }
      if (this.selectedStatus !== 'all' && this.reportStatus(row) !== this.selectedStatus) {
        return false;
      }
      return true;
    });

    this.loadMore(true);
  }

  private async ensureProfileFilled(): Promise<boolean> {
    let card: PersonalCard | null = null;
    try {
      card = await firstValueFrom(this.foodbotApi.getPersonalCard());
    } catch {
      this.sb.open('Не удалось проверить профиль. Повторите позже.', 'OK', { duration: 4000 });
      return false;
    }

    if (this.isProfileComplete(card)) {
      return true;
    }

    const ref = this.dialog.open(ProfileRequiredDialogComponent, { autoFocus: false });
    const action = await firstValueFrom(ref.afterClosed());
    if (action === 'profile') {
      this.router.navigate(['/profile']);
    }
    return false;
  }

  private isProfileComplete(card: PersonalCard | null): card is PersonalCard {
    if (!card) return false;
    const required: Array<keyof PersonalCard> = ['gender', 'birthYear', 'heightCm', 'weightKg', 'activityLevel'];
    return required.every(field => card[field] !== null && card[field] !== undefined);
  }
}
