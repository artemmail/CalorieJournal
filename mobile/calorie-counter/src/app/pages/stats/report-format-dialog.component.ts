import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatRadioModule } from '@angular/material/radio';
import { MatButtonModule } from '@angular/material/button';
import { ReportFormat } from '../../services/stats.service';

interface DialogData {
  format: ReportFormat;
}

@Component({
  selector: 'app-report-format-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatRadioModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Экспорт отчёта</h2>
    <mat-dialog-content>
      <p class="hint">Выберите формат файла.</p>
      <mat-radio-group [(ngModel)]="format">
        <mat-radio-button value="pdf">PDF</mat-radio-button>
        <mat-radio-button value="docx">DOCX</mat-radio-button>
      </mat-radio-group>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Отмена</button>
      <button mat-flat-button color="primary" (click)="confirm()">Экспорт</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-radio-group {
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-top: 8px;
      }

      .hint {
        margin: 0;
        color: rgba(0, 0, 0, 0.6);
        font-size: 13px;
      }
    `
  ]
})
export class ReportFormatDialogComponent {
  format: ReportFormat;

  constructor(
    private readonly ref: MatDialogRef<ReportFormatDialogComponent, ReportFormat | undefined>,
    @Inject(MAT_DIALOG_DATA) data: DialogData
  ) {
    this.format = data.format;
  }

  cancel() {
    this.ref.close();
  }

  confirm() {
    this.ref.close(this.format);
  }
}

