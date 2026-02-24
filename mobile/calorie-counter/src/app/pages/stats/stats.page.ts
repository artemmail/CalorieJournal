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
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { StatsService, DayStats, ReportFormat } from '../../services/stats.service';
import { ReportFormatDialogComponent } from './report-format-dialog.component';

type ViewMode = 'chart' | 'table';
type Period = 'week' | 'month' | 'quarter' | 'custom';

interface Totals {
  calories: number;
  proteins: number;
  fats: number;
  carbs: number;
}

interface DayRow {
  date: Date;
  totals: Totals;
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
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule
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
  periodCaption = '';

  loading = false;
  loadError = '';

  data: DayRow[] = [];
  maxCalories = 0;
  effectiveMax = 0;
  ticks: number[] = [];

  averageCalories = 0;
  peakCalories = 0;
  minimumCalories = 0;
  daysWithEntries = 0;
  daysInTargetPct: number | null = null;

  tableTotals: Totals = this.zeroTotals();

  legend = {
    proteinsPct: 0,
    fatsPct: 0,
    carbsPct: 0,
    totalMacroKcal: 0
  };

  private readonly calorieTargetRange: { min: number; max: number } | null = null;

  constructor(
    private readonly stats: StatsService,
    private readonly sb: MatSnackBar,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit() {
    this.updatePeriod();
  }

  get hasCaloriesData(): boolean {
    return this.data.some(d => d.totals.calories > 0);
  }

  get showSparseHint(): boolean {
    const nonZero = this.data.filter(d => d.totals.calories > 0).length;
    return nonZero > 0 && nonZero < 3;
  }

  selectPreset(period: Period | null) {
    if (!period) {
      return;
    }
    if (this.selectedPeriod === period) {
      return;
    }
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
      if (this.selectedPeriod === 'week') {
        start.setDate(end.getDate() - 6);
      } else if (this.selectedPeriod === 'month') {
        start.setDate(end.getDate() - 29);
      } else {
        start.setDate(end.getDate() - 89);
      }

      if (!preserveCustom) {
        this.customStart = new Date(start);
        this.customEnd = new Date(end);
      }
    }

    this.periodStart = start;
    this.periodEnd = end;
    this.periodCaption = this.formatPeriodCaption(start, end);

    this.loading = true;
    this.loadError = '';

    this.stats.getDaily(start, end).subscribe({
      next: res => {
        this.data = this.buildDaySeries(start, end, res);
        this.maxCalories = Math.max(0, ...this.data.map(d => d.totals.calories || 0));
        this.effectiveMax = this.maxCalories > 0 ? this.roundNice(this.maxCalories * 1.05) : 0;
        this.ticks = this.buildTicks(this.effectiveMax);

        this.recalcLegend();
        this.recalcKpis();
        this.recalcTotals();
        this.loading = false;
      },
      error: () => {
        this.loadError = 'Не удалось загрузить статистику за выбранный период.';
        this.data = [];
        this.ticks = [];
        this.tableTotals = this.zeroTotals();
        this.loading = false;
      }
    });
  }

  onRangeChange() {
    if (!this.customStart || !this.customEnd) {
      return;
    }

    const start = this.normalizeDate(this.customStart);
    const end = this.normalizeDate(this.customEnd);

    if (end < start) {
      return;
    }

    this.customStart = start;
    this.customEnd = end;
    this.selectedPeriod = 'custom';
    this.updatePeriod(true);
  }

  openExportDialog() {
    if (!this.periodStart || !this.periodEnd || this.loading) {
      return;
    }

    const ref = this.dialog.open(ReportFormatDialogComponent, {
      data: { format: this.reportFormat }
    });

    ref.afterClosed().subscribe(format => {
      if (!format) {
        return;
      }
      this.reportFormat = format;
      this.requestReport(format);
    });
  }

  trackByDay(_: number, row: DayRow): string {
    return this.toDateKey(row.date);
  }

  macroShare(d: DayRow, kind: 'p' | 'f' | 'c'): number {
    const pK = (d.totals.proteins || 0) * 4;
    const fK = (d.totals.fats || 0) * 9;
    const cK = (d.totals.carbs || 0) * 4;
    const sum = pK + fK + cK;
    if (!sum) {
      return 0;
    }
    if (kind === 'p') {
      return (pK / sum) * 100;
    }
    if (kind === 'f') {
      return (fK / sum) * 100;
    }
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

  private recalcLegend() {
    const pG = this.data.reduce((sum, d) => sum + (d.totals.proteins || 0), 0);
    const fG = this.data.reduce((sum, d) => sum + (d.totals.fats || 0), 0);
    const cG = this.data.reduce((sum, d) => sum + (d.totals.carbs || 0), 0);

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
      this.legend.proteinsPct = 0;
      this.legend.fatsPct = 0;
      this.legend.carbsPct = 0;
    }
  }

  private recalcKpis() {
    if (this.data.length === 0) {
      this.averageCalories = 0;
      this.peakCalories = 0;
      this.minimumCalories = 0;
      this.daysWithEntries = 0;
      this.daysInTargetPct = null;
      return;
    }

    const calories = this.data.map(d => d.totals.calories || 0);
    const sum = calories.reduce((acc, value) => acc + value, 0);

    this.averageCalories = sum / calories.length;
    this.peakCalories = Math.max(...calories);
    this.minimumCalories = Math.min(...calories);
    this.daysWithEntries = calories.filter(value => value > 0).length;

    if (this.calorieTargetRange) {
      const inTarget = calories.filter(
        value => value >= this.calorieTargetRange!.min && value <= this.calorieTargetRange!.max
      ).length;
      this.daysInTargetPct = (inTarget / calories.length) * 100;
    } else {
      this.daysInTargetPct = null;
    }
  }

  private recalcTotals() {
    this.tableTotals = this.data.reduce(
      (acc, day) => ({
        calories: acc.calories + (day.totals.calories || 0),
        proteins: acc.proteins + (day.totals.proteins || 0),
        fats: acc.fats + (day.totals.fats || 0),
        carbs: acc.carbs + (day.totals.carbs || 0)
      }),
      this.zeroTotals()
    );
  }

  private buildDaySeries(start: Date, end: Date, apiRows: DayStats[]): DayRow[] {
    const map = new Map<string, Totals>();

    for (const row of apiRows) {
      const parsedDate = this.parseApiDate(row.date);
      map.set(this.toDateKey(parsedDate), {
        calories: row.totals.calories || 0,
        proteins: row.totals.proteins || 0,
        fats: row.totals.fats || 0,
        carbs: row.totals.carbs || 0
      });
    }

    const result: DayRow[] = [];
    const cursor = new Date(start);

    while (cursor <= end) {
      const key = this.toDateKey(cursor);
      result.push({
        date: new Date(cursor),
        totals: map.get(key) ?? this.zeroTotals()
      });
      cursor.setDate(cursor.getDate() + 1);
    }

    return result;
  }

  private buildTicks(max: number): number[] {
    if (max <= 0) {
      return [];
    }
    const steps = 4;
    const values: number[] = [];

    for (let i = 0; i <= steps; i++) {
      values.push(this.roundNice(max * (i / steps)));
    }

    return Array.from(new Set(values));
  }

  private roundNice(value: number): number {
    if (value < 10) {
      return Math.round(value);
    }
    if (value < 100) {
      return Math.round(value / 5) * 5;
    }
    if (value < 1000) {
      return Math.round(value / 10) * 10;
    }
    return Math.round(value / 20) * 20;
  }

  private normalizeDate(date: Date): Date {
    const normalized = new Date(date);
    normalized.setHours(0, 0, 0, 0);
    return normalized;
  }

  private parseApiDate(value: string): Date {
    const isoDay = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (isoDay) {
      const year = Number(isoDay[1]);
      const month = Number(isoDay[2]) - 1;
      const day = Number(isoDay[3]);
      return new Date(year, month, day);
    }

    const parsed = new Date(value);
    if (isNaN(parsed.getTime())) {
      return new Date();
    }

    parsed.setHours(0, 0, 0, 0);
    return parsed;
  }

  private toDateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private formatPeriodCaption(start: Date, end: Date): string {
    const startLabel = this.formatDate(start);
    const endLabel = this.formatDate(end);
    return `${startLabel} - ${endLabel}`;
  }

  private formatDate(date: Date): string {
    const normalized = this.normalizeDate(date);
    return normalized.toLocaleDateString('ru-RU');
  }

  private zeroTotals(): Totals {
    return {
      calories: 0,
      proteins: 0,
      fats: 0,
      carbs: 0
    };
  }
}
