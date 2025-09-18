import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InfiniteScrollModule } from 'ngx-infinite-scroll';
import { AnalysisService, AnalysisPeriod, ReportRow } from '../../services/analysis.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    InfiniteScrollModule
  ],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage implements OnInit, OnDestroy {
  cols = ['date', 'name', 'period', 'status'];
  rows: ReportRow[] = [];
  allRows: ReportRow[] = [];
  pageSize = 10;
  loading = false;
  hasProcessing = false;

  private timer?: any;

  constructor(private api: AnalysisService, private sb: MatSnackBar, private router: Router) {}

  ngOnInit(): void {
    this.refresh();
    this.timer = setInterval(() => {
      if (this.hasProcessing) this.refresh(false);
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  async refresh(showSpinner = true) {
    try {
      if (showSpinner) this.loading = true;
      const list = await this.api.list();
      this.allRows = list;
      this.rows = [];
      this.loadMore();
      this.hasProcessing = list.some(x => x.isProcessing);
    } finally {
      this.loading = false;
    }
  }

  loadMore() {
    const next = this.allRows.slice(this.rows.length, this.rows.length + this.pageSize);
    if (next.length) this.rows = [...this.rows, ...next];
  }

  onScrollDown() { this.loadMore(); }

  async create(period: AnalysisPeriod) {
    try {
      this.loading = true;
      const res = await this.api.create(period);
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

  open(row: ReportRow) {
    this.router.navigate(['/analysis', row.id]);
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
}
