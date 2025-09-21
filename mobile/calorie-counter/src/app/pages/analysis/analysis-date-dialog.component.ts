import { Component, Inject, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';

interface DialogData {
  defaultDate?: Date;
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
    MatInputModule
  ],
  template: `
    <h2 mat-dialog-title>Создать отчёт от даты</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Дата</mat-label>
        <input matInput [matDatepicker]="picker" [(ngModel)]="selectedDate" />
        <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
        <mat-datepicker #picker></mat-datepicker>
      </mat-form-field>
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
      .full-width {
        width: 100%;
      }
    `
  ]
})
export class AnalysisDateDialogComponent {
  selectedDate: Date | null;

  constructor(
    private readonly dialogRef: MatDialogRef<AnalysisDateDialogComponent, Date | null>,
    @Optional() @Inject(MAT_DIALOG_DATA) data: DialogData | null
  ) {
    const initial = data?.defaultDate ? new Date(data.defaultDate) : new Date();
    initial.setHours(0, 0, 0, 0);
    this.selectedDate = initial;
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
    this.dialogRef.close(normalized);
  }
}
