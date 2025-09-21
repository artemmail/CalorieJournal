import { Component, Inject, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { AnalysisPeriod } from '../../services/analysis.service';

interface DialogData {
  defaultDate?: Date;
  defaultPeriod?: AnalysisPeriod;
}

interface DialogResult {
  date: Date;
  period: AnalysisPeriod;
}

@Component({
  selector: 'app-analysis-date-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>Создать отчёт от даты</h2>
    <mat-dialog-content>
      <div class="fields">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Тип отчёта</mat-label>
          <mat-select [(ngModel)]="selectedPeriod">
            <mat-option *ngFor="let option of periods" [value]="option.value">
              {{ option.label }}
            </mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Дата</mat-label>
          <input matInput [matDatepicker]="picker" [(ngModel)]="selectedDate" />
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Отмена</button>
      <button mat-flat-button color="primary" [disabled]="!selectedDate" (click)="confirm()">
        Создать
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .fields {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .full-width {
        width: 100%;
      }
    `
  ]
})
export class AnalysisDateDialogComponent {
  selectedDate: Date | null;
  selectedPeriod: AnalysisPeriod;

  readonly periods: Array<{ value: AnalysisPeriod; label: string }> = [
    { value: 'day', label: 'День · итог' },
    { value: 'dayRemainder', label: 'День · остаток' },
    { value: 'week', label: 'Неделя' },
    { value: 'month', label: 'Месяц' },
    { value: 'quarter', label: 'Квартал' }
  ];

  constructor(
    private readonly dialogRef: MatDialogRef<AnalysisDateDialogComponent, DialogResult | null>,
    @Optional() @Inject(MAT_DIALOG_DATA) data: DialogData | null
  ) {
    const initial = data?.defaultDate ? new Date(data.defaultDate) : new Date();
    initial.setHours(0, 0, 0, 0);
    this.selectedDate = initial;
    this.selectedPeriod = data?.defaultPeriod ?? 'day';
  }

  cancel() {
    this.dialogRef.close(null);
  }

  confirm() {
    if (!this.selectedDate) {
      return;
    }
    const normalized = new Date(this.selectedDate);
    normalized.setHours(0, 0, 0, 0);
    this.dialogRef.close({
      date: normalized,
      period: this.selectedPeriod
    });
  }
}
