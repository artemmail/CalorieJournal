import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { ClarifyResult } from '../../services/foodbot-api.types';
import { firstValueFrom } from 'rxjs';

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
    MatSnackBarModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title class="title">
      <span>Уточнение</span>
      <span class="mic-container">
        <button mat-icon-button color="primary"
                (mousedown)="startRecord($event)" (touchstart)="startRecord($event)"
                (mouseup)="stopRecord()" (mouseleave)="stopRecord()" (touchend)="stopRecord()"
                [disabled]="transcribing">
          <mat-icon>mic</mat-icon>
        </button>
        <div class="mic-hint">Удерживайте кнопку для записи</div>
      </span>
    </h2>
    <div mat-dialog-content>
      <mat-form-field appearance="fill" class="note-field">
        <mat-label>Текст</mat-label>
        <textarea matInput rows="5" [(ngModel)]="note"></textarea>
      </mat-form-field>
      <mat-form-field appearance="fill" class="time-field">
        <mat-label>Время</mat-label>
        <input matInput type="time" [(ngModel)]="time">
      </mat-form-field>
    </div>
    <div class="overlay" *ngIf="transcribing">
      <mat-spinner></mat-spinner>
    </div>
    <div mat-dialog-actions align="end">
      <button mat-button color="warn" (click)="remove()">Удалить</button>
      <button mat-button (click)="dialogRef.close()">Отмена</button>
      <button mat-raised-button color="primary" (click)="send()" [disabled]="!note.trim() && time === initialTime">Отправить</button>
    </div>
  `,
  styles: [`
    .note-field { width: 100%; }
    .note-field textarea { min-height: 120px; }
    .title { display: flex; align-items: center; }
    .mic-container { display: flex; flex-direction: column; align-items: center; margin-left: 8px; }
    .mic-hint { font-size: 12px; margin-top: 4px; text-align: center; color: rgba(0,0,0,0.6); }
    :host { display: block; position: relative; }
    .overlay {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(255, 255, 255, 0.6);
      z-index: 10;
    }
  `]
})
export class HistoryClarifyDialogComponent {
  note = '';
  time = '';
  transcribing = false;
  private recorder?: MediaRecorder;
  private chunks: Blob[] = [];
  private createdAt: Date;
  private initialTime: string;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { mealId: number; createdAtUtc: string },
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    public dialogRef: MatDialogRef<HistoryClarifyDialogComponent>
  ) {
    this.createdAt = new Date(data.createdAtUtc);
    this.time = this.createdAt.toISOString().substring(11, 16);
    this.initialTime = this.time;
  }

  async startRecord(ev: Event) {
    ev.preventDefault();
    if (this.transcribing || this.recorder) return;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.chunks = [];
      this.recorder = new MediaRecorder(stream);
      this.recorder.addEventListener('dataavailable', e => {
        if (e.data.size > 0) this.chunks.push(e.data);
      });
      this.recorder.addEventListener('stop', () => stream.getTracks().forEach(t => t.stop()), { once: true });
      this.recorder.start();
    } catch {
      this.recorder = undefined;
      this.snack.open('Не удалось получить доступ к микрофону', 'OK', { duration: 1500 });
    }
  }

  async stopRecord() {
    if (!this.recorder) return;
    const recorder = this.recorder;
    this.recorder = undefined;
    const stopped = new Promise<void>(resolve => recorder.addEventListener('stop', () => resolve(), { once: true }));
    recorder.stop();
    await stopped;
    const chunks = this.chunks.slice();
    this.transcribing = true;
    try {
      const blob = new Blob(chunks, { type: 'audio/webm' });
      const file = new File([blob], 'voice.webm', { type: blob.type });
      const r = await firstValueFrom(this.api.transcribeVoice(file));
      if (r.text) this.note = r.text;
    } catch {
      this.snack.open('Не удалось распознать речь', 'OK', { duration: 1500 });
    } finally {
      this.transcribing = false;
    }
  }

  send() {
    const note = this.note.trim();
    const timeChanged = this.time !== this.initialTime;
    if (!note && !timeChanged) return;
    let iso: string | undefined;
    if (timeChanged) {
      const [h, m] = this.time.split(':').map(v => parseInt(v, 10));
      const dt = new Date(this.createdAt);
      dt.setHours(h, m, 0, 0);
      iso = dt.toISOString();
    }
    this.api.clarifyText(this.data.mealId, note || undefined, iso).subscribe({
      next: (r: ClarifyResult) => {
        const createdAtUtc = iso ?? this.data.createdAtUtc;
        this.dialogRef.close({ ...r, createdAtUtc });
      },
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
