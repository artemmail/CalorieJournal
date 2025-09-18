import { Component, Inject, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { TextFieldModule } from "@angular/cdk/text-field";
import { firstValueFrom } from "rxjs";

import { FoodbotApiService } from "../../services/foodbot-api.service";
import { ClarifyResult } from "../../services/foodbot-api.types";
import { ConfirmDialogComponent, ConfirmDialogData } from "../confirm-dialog/confirm-dialog.component";

type AddMealVoiceNoteData = { title: string; kind: "addMeal" };
type HistoryVoiceNoteData = { title: string; kind: "historyClarify"; mealId: number; createdAtUtc: string; note?: string };

export type VoiceNoteDialogData = AddMealVoiceNoteData | HistoryVoiceNoteData;

@Component({
  selector: "app-voice-note-dialog",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressBarModule, MatSnackBarModule, MatTooltipModule,
    TextFieldModule
  ],
  templateUrl: "./voice-note-dialog.component.html",
  styleUrls: ["./voice-note-dialog.component.scss"]
})
export class VoiceNoteDialogComponent implements OnDestroy {
  form: FormGroup;
  transcribing = false;
  recorder?: MediaRecorder;
  private chunks: Blob[] = [];
  private createdAt?: Date;
  private initialTime?: string;
  private historyData?: HistoryVoiceNoteData;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: VoiceNoteDialogData,
    private fb: FormBuilder,
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<VoiceNoteDialogComponent>
  ) {
    if (data.kind === "historyClarify") {
      this.historyData = data;
      this.createdAt = new Date(data.createdAtUtc);
      const pad = (v: number) => v.toString().padStart(2, "0");
      const time = `${pad(this.createdAt.getHours())}:${pad(this.createdAt.getMinutes())}`;
      this.initialTime = time;
      this.form = this.fb.group({
        note: [data.note ?? ""],
        timestamp: [time]
      });
    } else {
      const now = new Date();
      this.form = this.fb.group({
        note: [""],
        timestamp: [this.formatDateTimeLocal(now)]
      });
    }
  }

  get noteControl() {
    return this.form.get("note");
  }

  get timestampControl() {
    return this.form.get("timestamp");
  }

  get isHistoryClarify(): boolean {
    return !!this.historyData;
  }

  get canSend(): boolean {
    if (this.transcribing) return false;
    const note = (this.noteControl?.value as string | null)?.trim() ?? "";
    if (this.isHistoryClarify) {
      const currentTime = (this.timestampControl?.value as string | null) ?? "";
      const timeChanged = currentTime !== (this.initialTime ?? "");
      return note.length > 0 || timeChanged;
    }
    return note.length > 0;
  }

  get previewNote(): string {
    return ((this.noteControl?.value as string | null) ?? "").trim();
  }

  get previewTime(): Date | null {
    if (this.isHistoryClarify) {
      if (!this.createdAt) return null;
      const base = new Date(this.createdAt);
      const currentTime = (this.timestampControl?.value as string | null)?.trim();
      if (!currentTime) return base;
      const [hoursStr, minutesStr] = currentTime.split(":");
      const hours = Number.parseInt(hoursStr ?? "", 10);
      const minutes = Number.parseInt(minutesStr ?? "", 10);
      if (Number.isNaN(hours) || Number.isNaN(minutes)) return base;
      const dt = new Date(base);
      dt.setHours(hours, minutes, 0, 0);
      return dt;
    }
    const value = (this.timestampControl?.value as string | null)?.trim();
    if (!value) return null;
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  clearNote() {
    this.noteControl?.setValue("");
  }

  close() {
    this.dialogRef.close();
  }

  async startRecord(ev: Event) {
    ev.preventDefault();
    if (this.transcribing || this.recorder) return;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.chunks = [];
      const recorder = new MediaRecorder(stream);
      recorder.addEventListener("dataavailable", e => {
        if (e.data.size > 0) this.chunks.push(e.data);
      });
      recorder.addEventListener("stop", () => {
        stream.getTracks().forEach(t => t.stop());
      }, { once: true });
      recorder.start();
      this.recorder = recorder;
    } catch {
      this.recorder = undefined;
      this.snack.open("Не удалось получить доступ к микрофону", "OK", { duration: 1500 });
    }
  }

  async stopRecord() {
    if (!this.recorder) return;
    const recorder = this.recorder;
    this.recorder = undefined;
    const stopped = new Promise<void>(resolve =>
      recorder.addEventListener("stop", () => resolve(), { once: true })
    );
    recorder.stop();
    await stopped;
    const chunks = this.chunks.slice();
    if (!chunks.length) return;

    this.transcribing = true;
    try {
      const blob = new Blob(chunks, { type: "audio/webm" });
      const file = new File([blob], "voice.webm", { type: blob.type });
      const r = await firstValueFrom(this.api.transcribeVoice(file));
      if (r.text) this.noteControl?.setValue(r.text);
    } catch {
      this.snack.open("Не удалось распознать речь", "OK", { duration: 1500 });
    } finally {
      this.transcribing = false;
    }
  }

  send() {
    if (!this.canSend) return;
    if (this.isHistoryClarify) {
      this.sendHistoryClarify();
    } else {
      this.sendAddMeal();
    }
  }

  private sendAddMeal() {
    const note = ((this.noteControl?.value as string | null) ?? "").trim();
    if (!note) return;
    const time = this.buildTimestamp();
    this.api.addMealText(note, true, time).subscribe({
      next: () => {
        this.snack.open("Описание отправлено. Обработка начнётся скоро.", "OK", { duration: 1500 });
        const now = new Date();
        this.form.reset({ note: "", timestamp: this.formatDateTimeLocal(now) });
        this.dialogRef.close(true);
      },
      error: () => {
        this.snack.open("Не удалось отправить описание", "OK", { duration: 2000 });
      }
    });
  }

  private sendHistoryClarify() {
    const data = this.historyData;
    if (!data || !this.createdAt) return;
    const note = ((this.noteControl?.value as string | null) ?? "").trim();
    const currentTime = (this.timestampControl?.value as string | null) ?? "";
    const timeChanged = currentTime !== (this.initialTime ?? "");
    if (!note && !timeChanged) return;

    let iso: string | undefined;
    if (timeChanged) {
      const [hoursStr, minutesStr] = currentTime.split(":");
      const hours = Number.parseInt(hoursStr ?? "0", 10);
      const minutes = Number.parseInt(minutesStr ?? "0", 10);
      const dt = new Date(this.createdAt);
      if (!Number.isNaN(hours) && !Number.isNaN(minutes)) {
        dt.setHours(hours, minutes, 0, 0);
        iso = dt.toISOString();
      }
    }

    this.api.clarifyText(data.mealId, note || undefined, iso).subscribe({
      next: (r: ClarifyResult | { queued: boolean }) => {
        if ((r as any).queued) {
          const queuedTime = iso ?? data.createdAtUtc;
          this.dialogRef.close({ queued: true, note, time: queuedTime });
          return;
        }
        const res = r as ClarifyResult;
        const createdAtUtc = iso ?? data.createdAtUtc;
        this.dialogRef.close({ ...res, createdAtUtc, note });
      },
      error: () => {
        this.snack.open("Ошибка уточнения", "OK", { duration: 1500 });
      }
    });
  }

  remove() {
    const data = this.historyData;
    if (!data) return;
    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data: {
        title: "Удаление записи",
        message: "Удалить запись?",
        confirmLabel: "Удалить",
        cancelLabel: "Отмена"
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.api.deleteMeal(data.mealId).subscribe({
        next: () => this.dialogRef.close({ deleted: true }),
        error: () => this.snack.open("Не удалось удалить", "OK", { duration: 1500 })
      });
    });
  }

  ngOnDestroy() {
    if (this.recorder && this.recorder.state !== "inactive") {
      try {
        this.recorder.stop();
      } catch {
        // ignore
      }
    }
    this.recorder = undefined;
    this.chunks = [];
  }

  private formatDateTimeLocal(date: Date): string {
    const pad = (v: number) => v.toString().padStart(2, "0");
    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  private buildTimestamp(): string | undefined {
    const value = (this.timestampControl?.value as string | null)?.trim();
    if (!value) return undefined;

    const [datePart, timePart] = value.split("T");
    if (!datePart || !timePart) return undefined;

    const [yearStr, monthStr, dayStr] = datePart.split("-");
    const timeSections = timePart.split(":");
    if (timeSections.length < 2) return undefined;

    const hours = Number.parseInt(timeSections[0] ?? "0", 10);
    const minutes = Number.parseInt(timeSections[1] ?? "0", 10);
    const secondsRaw = timeSections[2]?.split(".")[0] ?? "0";
    const seconds = Number.parseInt(secondsRaw, 10) || 0;

    const year = Number.parseInt(yearStr ?? "0", 10);
    const month = Number.parseInt(monthStr ?? "0", 10);
    const day = Number.parseInt(dayStr ?? "0", 10);

    if (!year || !month || !day) return undefined;
    if (Number.isNaN(hours) || Number.isNaN(minutes) || Number.isNaN(seconds)) return undefined;

    const local = new Date(year, month - 1, day, hours, minutes, seconds);
    if (Number.isNaN(local.getTime())) return undefined;

    const offsetMinutes = -local.getTimezoneOffset();
    const pad = (v: number) => v.toString().padStart(2, "0");
    const sign = offsetMinutes >= 0 ? "+" : "-";
    const absOffset = Math.abs(offsetMinutes);
    const offsetHours = Math.floor(absOffset / 60);
    const offsetMins = absOffset % 60;
    const offsetStr = `${sign}${pad(offsetHours)}:${pad(offsetMins)}`;
    const timeStr = `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    return `${datePart}T${timeStr}${offsetStr}`;
  }
}
