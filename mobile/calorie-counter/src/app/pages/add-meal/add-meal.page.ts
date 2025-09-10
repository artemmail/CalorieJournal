import { Component, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpEventType } from "@angular/common/http";
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { firstValueFrom } from "rxjs";

import { Camera, CameraResultType, CameraSource } from "@capacitor/camera";
import { CameraPreview, CameraPreviewOptions, CameraPreviewPictureOptions } from "@capacitor-community/camera-preview";

import { FoodbotApiService } from "../../services/foodbot-api.service";

function b64toFile(base64: string, name: string, type = "image/jpeg"): File {
  const byteString = atob(base64);
  const ab = new ArrayBuffer(byteString.length);
  const ia = new Uint8Array(ab);
  for (let i = 0; i < byteString.length; i++) ia[i] = byteString.charCodeAt(i);
  const blob = new Blob([ab], { type });
  return new File([blob], name, { type });
}

@Component({
  selector: "app-add-meal",
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
    MatProgressBarModule, MatSnackBarModule, MatTooltipModule
  ],
  templateUrl: "./add-meal.page.html",
  styleUrls: ["./add-meal.page.scss"]
})
export class AddMealPage implements OnDestroy {
  photoDataUrl?: string;    // превью для UI (через pipe convert)
  form!: FormGroup;
  previewActive = false;
  transcribing = false;
  recorder?: MediaRecorder;
  private chunks: Blob[] = [];

  uploadProgress: number | null = null;
  progressMode: "determinate" | "indeterminate" = "determinate";

  constructor(private fb: FormBuilder, private api: FoodbotApiService, private snack: MatSnackBar) {
    this.form = this.fb.group({
      note: [""]
    });
  }

  // системная камера
  async takePhotoSystem() {
    const img = await Camera.getPhoto({ quality: 80, resultType: CameraResultType.Base64, source: CameraSource.Camera });
    if (img.base64String) {
      await this.uploadBase64(img.base64String);
    }
  }

  // превью
  async startPreview() {
    if (this.previewActive) return;
    const opts: CameraPreviewOptions = {
      parent: "cameraPreview",
      className: "cameraPreview",
      position: "rear",
      disableAudio: true,
      toBack: false,
      width: window.innerWidth,
      height: 360
    };
    await CameraPreview.start(opts);
    this.previewActive = true;
  }
  async stopPreview() { if (this.previewActive) { await CameraPreview.stop(); this.previewActive = false; } }
  async captureFromPreview() {
    const picOpts: CameraPreviewPictureOptions = { quality: 90 };
    const r = await CameraPreview.capture(picOpts); // { value: base64 }
    await this.stopPreview();
    if (r?.value) await this.uploadBase64(r.value);
  }

  async uploadBase64(b64: string) {
    // превью
    this.photoDataUrl = "data:image/jpeg;base64," + b64;
    // upload
    const file = b64toFile(b64, `meal_${Date.now()}.jpg`);
    this.uploadProgress = 0;
    this.progressMode = "determinate";
    this.api.uploadPhoto(file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress = Math.round(100 * event.loaded / event.total);
          if (event.loaded === event.total) this.progressMode = "indeterminate";
        } else if (event.type === HttpEventType.Response) {
          this.uploadProgress = null;
          this.snack.open("Фото отправлено. Обработка начнётся скоро.", "OK", { duration: 1500 });
        }
      },
      error: () => {
        this.uploadProgress = null;
        this.snack.open("Не удалось отправить фото", "OK", { duration: 2000 });
      }
    });
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
      if (r.text) this.form.controls['note'].setValue(r.text);
    } catch {
      this.snack.open('Не удалось распознать речь', 'OK', { duration: 1500 });
    } finally {
      this.transcribing = false;
    }
  }

  sendText() {
    const note: string = this.form.value.note?.trim();
    if (!note) return;
    this.api.addMealText(note).subscribe({
      next: () => {
        this.snack.open('Описание отправлено. Обработка начнётся скоро.', 'OK', { duration: 1500 });
        this.form.reset({ note: '' });
      },
      error: () => {
        this.snack.open('Не удалось отправить описание', 'OK', { duration: 2000 });
      }
    });
  }

  async ngOnDestroy() { await this.stopPreview(); }
}

