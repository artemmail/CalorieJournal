import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild } from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpEventType } from "@angular/common/http";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";

import { Camera, CameraResultType, CameraSource } from "@capacitor/camera";
import { CameraPreview, CameraPreviewOptions, CameraPreviewPictureOptions } from "@capacitor-community/camera-preview";

import { FoodbotApiService } from "../../services/foodbot-api.service";
import { AddMealNoteDialogComponent } from "./add-meal-note-dialog.component";

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
    CommonModule,
    MatButtonModule, MatIconModule, MatProgressBarModule,
    MatSnackBarModule, MatTooltipModule, MatDialogModule
  ],
  templateUrl: "./add-meal.page.html",
  styleUrls: ["./add-meal.page.scss"]
})
export class AddMealPage implements AfterViewInit, OnDestroy {
  photoDataUrl?: string;    // превью для UI (через pipe convert)
  previewActive = false;
  uploadProgress: number | null = null;
  progressMode: "determinate" | "indeterminate" = "determinate";

  private previewStarting = false;

  @ViewChild("previewBox")
  private previewBox?: ElementRef<HTMLDivElement>;

  constructor(private api: FoodbotApiService, private snack: MatSnackBar, private dialog: MatDialog) {}

  async ngAfterViewInit() {
    await this.startPreviewWithFallback();
  }

  async ngOnDestroy() {
    await this.stopPreview();
  }

  // системная камера
  async takePhotoSystem() {
    try {
      const img = await Camera.getPhoto({ quality: 80, resultType: CameraResultType.Base64, source: CameraSource.Camera });
      if (img.base64String) {
        await this.uploadBase64(img.base64String);
      }
    } catch (err) {
      this.handleCameraError(err, "Не удалось сделать фото");
    }
  }

  openNoteDialog() {
    const dialogRef = this.dialog.open(AddMealNoteDialogComponent, {
      width: "min(480px, 90vw)",
      maxWidth: "90vw",
      autoFocus: false,
      restoreFocus: false
    });

    dialogRef.afterClosed().subscribe(() => {
      if (!this.previewActive) {
        void this.startPreviewWithFallback();
      }
    });
  }

  retryPreview() {
    void this.startPreviewWithFallback();
  }

  // превью
  private async startPreview() {
    if (this.previewActive || this.previewStarting) return;
    this.previewStarting = true;
    const { width, height } = this.resolvePreviewSize();
    const opts: CameraPreviewOptions = {
      parent: "cameraPreview",
      className: "cameraPreview",
      position: "rear",
      disableAudio: true,
      toBack: false,
      width,
      height
    };
    try {
      await CameraPreview.start(opts);
      this.previewActive = true;
    } finally {
      this.previewStarting = false;
    }
  }

  private resolvePreviewSize() {
    const fallback = {
      width: Math.max(1, Math.round(window.innerWidth || 0)),
      height: Math.max(1, Math.round(window.innerHeight || 0))
    };
    const el = this.previewBox?.nativeElement;
    if (!el) return fallback;
    const rect = el.getBoundingClientRect();
    const width = Math.round(rect.width);
    const height = Math.round(rect.height);
    return {
      width: width > 0 ? width : fallback.width,
      height: height > 0 ? height : fallback.height
    };
  }

  private async startPreviewWithFallback() {
    try {
      await this.startPreview();
    } catch (err) {
      this.previewActive = false;
      if (this.isPermissionError(err)) {
        this.snack.open("Доступ к камере не предоставлен. Откроется системная камера.", "OK", { duration: 2500 });
      } else {
        this.snack.open("Не удалось запустить превью камеры", "OK", { duration: 2000 });
      }
      await this.takePhotoSystem();
    }
  }

  async stopPreview() {
    if (!this.previewActive && !this.previewStarting) return;
    try {
      await CameraPreview.stop();
    } catch (err) {
      if (!this.isPermissionError(err)) {
        this.snack.open("Не удалось остановить превью камеры", "OK", { duration: 2000 });
      }
    } finally {
      this.previewActive = false;
      this.previewStarting = false;
    }
  }

  async captureFromPreview() {
    const picOpts: CameraPreviewPictureOptions = { quality: 90 };
    try {
      const r = await CameraPreview.capture(picOpts); // { value: base64 }
      if (r?.value) await this.uploadBase64(r.value);
    } catch (err) {
      this.handleCameraError(err, "Не удалось сделать фото");
    }
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

  private handleCameraError(err: unknown, fallbackMessage: string) {
    if (this.isPermissionError(err)) {
      this.snack.open("Доступ к камере не предоставлен", "OK", { duration: 2000 });
    } else {
      this.snack.open(fallbackMessage, "OK", { duration: 2000 });
    }
  }

  private isPermissionError(err: unknown): boolean {
    if (!err) return false;
    const maybeMessage = (err as { message?: string; errorMessage?: string }).message ?? (err as { message?: string; errorMessage?: string }).errorMessage;
    return typeof maybeMessage === "string" && /denied|perm/i.test(maybeMessage);
  }
}
