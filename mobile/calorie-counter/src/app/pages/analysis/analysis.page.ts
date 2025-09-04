import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AnalysisService, AnalysisPeriod, ReportRow } from '../../services/analysis.service';

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
    MatSnackBarModule
  ],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage implements OnInit, OnDestroy {
  cols = ['date', 'name', 'period', 'status', 'actions'];
  rows: ReportRow[] = [];
  loading = false;
  hasProcessing = false;

  private timer?: any;

  constructor(private api: AnalysisService, private sb: MatSnackBar) {}

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
      this.rows = list;
      this.hasProcessing = list.some(x => x.isProcessing);
    } finally {
      this.loading = false;
    }
  }

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
    window.open(`/analysis/${row.id}`, '_blank');
  }

  periodLabel(p: AnalysisPeriod) {
    switch (p) {
      case 'day': return 'День';
      case 'week': return 'Неделя';
      case 'month': return 'Месяц';
      case 'quarter': return 'Квартал';
    }
  }
}
