import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { StatsService, ReportFormat } from '../../services/stats.service';
import { ReportFormatDialogComponent } from './report-format-dialog.component';

type ViewMode = 'chart' | 'table';
type Period = 'week' | 'month' | 'quarter' | 'custom';

interface DayRow {
  date: Date;
  totals: { calories: number; proteins: number; fats: number; carbs: number };
}

@Component({
  selector: 'app-stats',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatInputModule,
    MatDialogModule
  ],
  templateUrl: './stats.page.html',
  styleUrls: ['./stats.page.scss']
})
export class StatsPage implements OnInit {
  view: ViewMode = 'chart';
  selectedPeriod: Period = 'quarter';
  customStart: Date | null = null;
  customEnd: Date | null = null;

  reportFormat: ReportFormat = 'pdf';

  periodStart!: Date;
  periodEnd!: Date;

  data: DayRow[] = [];
  maxCalories = 0;
  /** max + 5% для визуального отступа справа */
  effectiveMax = 0;

  /** Тики шкалы в ккал (0,25,50,75,100% от effectiveMax, округлено) */
  ticks: number[] = [];

  /** Легенда по периоду (проценты Б/Ж/У по ккал) */
  legend = {
    proteinsPct: 0,
    fatsPct: 0,
    carbsPct: 0,
    totalMacroKcal: 0
  };

  constructor(
    private readonly stats: StatsService,
    private readonly sb: MatSnackBar,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit() {
    this.updatePeriod();
  }

  selectPreset(period: Period | null) {
    if (!period) return;
    if (this.selectedPeriod === period) return;
    this.selectedPeriod = period;
    this.updatePeriod();
  }

  updatePeriod(preserveCustom = false) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    let start = new Date(today);
    let end = new Date(today);

    if (this.selectedPeriod === 'custom') {
      if (!this.customStart || !this.customEnd) {
        return;
      }
      start = this.normalizeDate(this.customStart);
      end = this.normalizeDate(this.customEnd);
    } else {
      if (this.selectedPeriod === 'week') start.setDate(end.getDate() - 6);
      else if (this.selectedPeriod === 'month') start.setDate(end.getDate() - 29);
      else start.setDate(end.getDate() - 89);

      if (!preserveCustom) {
        this.customStart = new Date(start);
        this.customEnd = new Date(end);
      }
    }

    this.periodStart = start;
    this.periodEnd = end;

    this.stats.getDaily(start, end).subscribe(res => {
      this.data = res.map(d => ({ date: new Date(d.date), totals: d.totals }));

      this.maxCalories = Math.max(0, ...this.data.map(d => d.totals.calories || 0));
      // 5% запас справа, чуть округляем
      this.effectiveMax = this.maxCalories > 0 ? this.roundNice(this.maxCalories * 1.05) : 0;

      this.ticks = this.buildTicks(this.effectiveMax);
      this.recalcLegend();
    });
  }

  onRangeChange() {
    if (!this.customStart || !this.customEnd) return;
    const start = this.normalizeDate(this.customStart);
    const end = this.normalizeDate(this.customEnd);
    if (end < start) return;

    this.customStart = start;
    this.customEnd = end;
    this.selectedPeriod = 'custom';
    this.updatePeriod(true);
  }

  openExportDialog() {
    if (!this.periodStart || !this.periodEnd) return;

    const ref = this.dialog.open(ReportFormatDialogComponent, {
      data: { format: this.reportFormat }
    });

    ref.afterClosed().subscribe(format => {
      if (!format) return;
      this.reportFormat = format;
      this.requestReport(format);
    });
  }

  private requestReport(format: ReportFormat) {
    this.stats.requestReport(format, this.periodStart, this.periodEnd).subscribe({
      next: () =>
        this.sb.open(
          `Отчёт (${format.toUpperCase()}) поставлен в очередь. Готовый файл придёт в Telegram.`,
          'OK',
          { duration: 3000 }
        ),
      error: () =>
        this.sb.open('Не удалось поставить отчёт в очередь.', 'Закрыть', { duration: 4000 })
    });
  }

  /** Ширина каждого сегмента (в % от текущего бара) */
  macroShare(d: DayRow, kind: 'p' | 'f' | 'c'): number {
    const pK = (d.totals.proteins || 0) * 4;
    const fK = (d.totals.fats || 0) * 9;
    const cK = (d.totals.carbs || 0) * 4;
    const sum = pK + fK + cK;
    if (!sum) return 0;
    if (kind === 'p') return (pK / sum) * 100;
    if (kind === 'f') return (fK / sum) * 100;
    return (cK / sum) * 100;
  }

  macroTooltip(d: DayRow, kind: 'p' | 'f' | 'c'): string {
    const grams =
      kind === 'p' ? d.totals.proteins || 0 : kind === 'f' ? d.totals.fats || 0 : d.totals.carbs || 0;
    const kcal = kind === 'p' ? grams * 4 : kind === 'f' ? grams * 9 : grams * 4;
    const pct = this.macroShare(d, kind);
    const label = kind === 'p' ? 'Белки' : kind === 'f' ? 'Жиры' : 'Углеводы';
    return `${label}: ${grams.toFixed(0)} г • ${kcal.toFixed(0)} ккал • ${pct.toFixed(0)}%`;
  }

  private recalcLegend() {
    const pG = this.data.reduce((s, d) => s + (d.totals.proteins || 0), 0);
    const fG = this.data.reduce((s, d) => s + (d.totals.fats || 0), 0);
    const cG = this.data.reduce((s, d) => s + (d.totals.carbs || 0), 0);

    const pK = pG * 4;
    const fK = fG * 9;
    const cK = cG * 4;

    const total = pK + fK + cK;
    this.legend.totalMacroKcal = total;

    if (total > 0) {
      this.legend.proteinsPct = (pK / total) * 100;
      this.legend.fatsPct = (fK / total) * 100;
      this.legend.carbsPct = (cK / total) * 100;
    } else {
      this.legend.proteinsPct = this.legend.fatsPct = this.legend.carbsPct = 0;
    }
  }

  private buildTicks(max: number): number[] {
    if (max <= 0) return [];
    const steps = 4; // 0..4 -> 5 меток
    const vals: number[] = [];
    for (let i = 0; i <= steps; i++) {
      vals.push(this.roundNice(max * (i / steps)));
    }
    // Убираем возможные дубли после округления
    return Array.from(new Set(vals));
  }

  private roundNice(x: number): number {
    if (x < 10) return Math.round(x);
    if (x < 100) return Math.round(x / 5) * 5;
    if (x < 1000) return Math.round(x / 10) * 10;
    return Math.round(x / 20) * 20;
  }

  private normalizeDate(date: Date): Date {
    const normalized = new Date(date);
    normalized.setHours(0, 0, 0, 0);
    return normalized;
  }
}
