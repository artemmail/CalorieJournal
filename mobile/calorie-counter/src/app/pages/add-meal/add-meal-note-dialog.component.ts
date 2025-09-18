import { Component, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { firstValueFrom } from "rxjs";

import { FoodbotApiService } from "../../services/foodbot-api.service";

@Component({
  selector: "app-add-meal-note-dialog",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatProgressBarModule, MatSnackBarModule, MatTooltipModule
  ],
  templateUrl: "./add-meal-note-dialog.component.html",
  styleUrls: ["./add-meal-note-dialog.component.scss"]
})
export class AddMealNoteDialogComponent implements OnDestroy {
  form: FormGroup;
  transcribing = false;
  recorder?: MediaRecorder;
  private chunks: Blob[] = [];

  constructor(
    private fb: FormBuilder,
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private dialogRef: MatDialogRef<AddMealNoteDialogComponent>
  ) {
    const now = new Date();
    this.form = this.fb.group({
      note: [""],
      dateTime: [this.formatDateTimeLocal(now)]
    });
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

  get noteControl() {
    return this.form.get("note");
  }

  get canSend(): boolean {
    const value = this.noteControl?.value as string | null;
    return !!value && value.trim().length > 0 && !this.transcribing;
  }

  close() {
    this.dialogRef.close();
  }

  clearNote() {
    this.noteControl?.setValue("");
  }

  private buildTimestamp(): string | undefined {
    const control = this.form.get("dateTime");
    const value = (control?.value as string | null)?.trim();
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

  sendText() {
    if (!this.canSend) return;
    const note = (this.noteControl?.value as string).trim();
    const time = this.buildTimestamp();
    this.api.addMealText(note, true, time).subscribe({
      next: () => {
        this.snack.open("Описание отправлено. Обработка начнётся скоро.", "OK", { duration: 1500 });
        this.form.reset({ note: "", dateTime: this.formatDateTimeLocal(new Date()) });
        this.dialogRef.close(true);
      },
      error: () => {
        this.snack.open("Не удалось отправить описание", "OK", { duration: 2000 });
      }
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
}
