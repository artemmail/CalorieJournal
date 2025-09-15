import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TextFieldModule } from '@angular/cdk/text-field';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { ClarifyResult } from '../../services/foodbot-api.types';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../components/confirm-dialog/confirm-dialog.component';
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
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatTooltipModule,
    TextFieldModule
  ],
  template: `
    <h2 mat-dialog-title class="header">
      <span class="title">Уточнение</span>

      <span class="mic">
        <button mat-icon-button color="primary"
                matTooltip="Удерживайте для записи"
                aria-label="Записать голос"
                (mousedown)="startRecord($event)" (touchstart)="startRecord($event)"
                (mouseup)="stopRecord()" (mouseleave)="stopRecord()" (touchend)="stopRecord()"
                [class.recording]="!!recorder" [disabled]="transcribing">
          <mat-icon>{{ recorder ? 'mic' : 'mic_none' }}</mat-icon>
        </button>
        <div class="hint" *ngIf="!recorder">Удерживайте для записи</div>
        <div class="hint recording" *ngIf="recorder">Запись… отпустите, чтобы остановить</div>
      </span>
    </h2>

    <mat-progress-bar *ngIf="transcribing" mode="indeterminate"></mat-progress-bar>

    <div mat-dialog-content class="content">
      <mat-form-field appearance="outline" class="note-field">
        <mat-label>Текст</mat-label>
        <textarea matInput
                  cdkTextareaAutosize
                  cdkAutosizeMinRows="4"
                  cdkAutosizeMaxRows="12"
                  [(ngModel)]="note"></textarea>
        <button *ngIf="note" mat-icon-button matSuffix aria-label="Очистить" (click)="note = ''">
          <mat-icon>close</mat-icon>
        </button>
      </mat-form-field>

      <mat-form-field appearance="outline" class="time-field">
        <mat-label>Время</mat-label>
        <input matInput type="time" [(ngModel)]="time">
        <mat-icon matSuffix>schedule</mat-icon>
      </mat-form-field>
    </div>

    <div mat-dialog-actions class="actions">
      <button mat-button color="warn" (click)="remove()">Удалить</button>
      <span class="spacer"></span>
      <button mat-button (click)="dialogRef.close()">Отмена</button>
      <button mat-flat-button color="primary"
              (click)="send()"
              [disabled]="!note.trim() && time === initialTime">Отправить</button>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      max-width: 640px;
    }

    /* ---------- header ---------- */
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .title { font-weight: 600; }
    .mic {
      display: grid;
      justify-items: center;
    }
    .mic .hint {
      font-size: 12px;
      opacity: .7;
      margin-top: 2px;
      text-align: center;
      line-height: 1.1;
    }
    .mic .hint.recording { opacity: .9; }
    .mic button.recording {
      animation: pulse 1s ease-in-out infinite;
    }
    @keyframes pulse {
      0%   { box-shadow: 0 0 0 0 rgba(244, 67, 54, .45); }
      70%  { box-shadow: 0 0 0 12px rgba(244, 67, 54, 0); }
      100% { box-shadow: 0 0 0 0 rgba(244, 67, 54, 0); }
    }

    /* ---------- content ---------- */
    .content {
      display: grid;
      grid-template-columns: 1fr;
      gap: 12px;
    }
    .note-field textarea { min-height: 116px; }

    .time-field { width: 200px; }

    @media (min-width: 560px) {
      .content {
        grid-template-columns: 1fr auto;
        align-items: start;
      }
      .note-field {
        grid-column: 1 / -1; /* текст во всю ширину */
      }
    }

    /* ---------- actions ---------- */
    .actions {
      display: flex;
      align-items: center;
      gap: 8px;
      padding-top: 8px;
    }
    .actions .spacer { flex: 1; }
  `]
})
export class HistoryClarifyDialogComponent {
  note = '';
  time = '';
  transcribing = false;
  recorder?: MediaRecorder;
  private chunks: Blob[] = [];
  private createdAt: Date;
  readonly initialTime: string;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { mealId: number; createdAtUtc: string; note?: string },
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private dialog: MatDialog,
    public dialogRef: MatDialogRef<HistoryClarifyDialogComponent>
  ) {
    this.createdAt = new Date(data.createdAtUtc);
    const pad = (n: number) => n.toString().padStart(2, '0');
    this.time = `${pad(this.createdAt.getHours())}:${pad(this.createdAt.getMinutes())}`;
    this.initialTime = this.time;
    this.note = data.note ?? '';
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
      this.recorder.addEventListener('stop', () =>
        stream.getTracks().forEach(t => t.stop()), { once: true });
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
    const stopped = new Promise<void>(resolve =>
      recorder.addEventListener('stop', () => resolve(), { once: true }));
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
      next: (r: ClarifyResult | { queued: boolean }) => {
        if ((r as any).queued) {
          this.dialogRef.close({ queued: true, note });
          return;
        }
        const res = r as ClarifyResult;
        const createdAtUtc = iso ?? this.data.createdAtUtc;
        this.dialogRef.close({ ...res, createdAtUtc, note });
      },
      error: () => {
        this.snack.open('Ошибка уточнения', 'OK', { duration: 1500 });
      }
    });
  }

  remove() {
    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data: {
        title: 'Удаление записи',
        message: 'Удалить запись?',
        confirmLabel: 'Удалить',
        cancelLabel: 'Отмена'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.api.deleteMeal(this.data.mealId).subscribe({
        next: () => this.dialogRef.close({ deleted: true }),
        error: () => this.snack.open('Не удалось удалить', 'OK', { duration: 1500 })
      });
    });
  }
}
 