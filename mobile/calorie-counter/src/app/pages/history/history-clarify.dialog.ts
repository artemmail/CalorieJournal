import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { ClarifyResult } from '../../services/foodbot-api.types';
import { VoiceService } from '../../services/voice.service';

@Component({
  selector: 'app-history-clarify-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSnackBarModule
  ],
  template: `
    <h2 mat-dialog-title>Уточнение</h2>
    <div mat-dialog-content class="content">
      <mat-form-field appearance="fill" class="note-field">
        <mat-label>Текст</mat-label>
        <textarea matInput [(ngModel)]="note"></textarea>
      </mat-form-field>
      <button mat-icon-button color="primary" (click)="speak()" [disabled]="loadingVoice">
        <mat-icon>mic</mat-icon>
      </button>
    </div>
    <div mat-dialog-actions align="end">
      <button mat-button color="warn" (click)="remove()">Удалить</button>
      <button mat-button (click)="dialogRef.close()">Отмена</button>
      <button mat-raised-button color="primary" (click)="send()" [disabled]="!note.trim()">Отправить</button>
    </div>
  `,
  styles: [`
    .content { display: flex; align-items: flex-start; gap: 8px; }
    .note-field { flex: 1; }
  `]
})
export class HistoryClarifyDialogComponent {
  note = '';
  loadingVoice = false;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { mealId: number },
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private voice: VoiceService,
    public dialogRef: MatDialogRef<HistoryClarifyDialogComponent>
  ) {}

  async speak() {
    this.loadingVoice = true;
    try {
      const text = await this.voice.listenOnce('ru-RU');
      if (text) this.note = text;
    } catch {
      this.snack.open('Не удалось распознать речь', 'OK', { duration: 1500 });
    } finally {
      this.loadingVoice = false;
    }
  }

  send() {
    const note = this.note.trim();
    if (!note) return;
    this.api.clarifyText(this.data.mealId, note).subscribe({
      next: (r: ClarifyResult) => this.dialogRef.close(r),
      error: () => {
        this.snack.open('Ошибка уточнения', 'OK', { duration: 1500 });
      }
    });
  }

  remove() {
    if (!confirm('Удалить запись?')) return;
    this.api.deleteMeal(this.data.mealId).subscribe({
      next: () => this.dialogRef.close({ deleted: true }),
      error: () => {
        this.snack.open('Не удалось удалить', 'OK', { duration: 1500 });
      }
    });
  }
}
