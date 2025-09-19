// =============================
// File: add-meal.page.ts
// =============================
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from "@angular/core";
import { CommonModule } from "@angular/common";
import { HttpEventType } from "@angular/common/http";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";

import { Camera, CameraResultType, CameraSource, type CameraPermissionState, type CameraPermissionType } from "@capacitor/camera";

import { firstValueFrom, Subscription } from "rxjs";

import { FoodbotApiService } from "../../services/foodbot-api.service";
import { VoiceNoteDialogComponent } from "../../components/voice-note-dialog/voice-note-dialog.component";
import { ClarifyResult, MealDetails, MealListItem } from "../../services/foodbot-api.types";
import { HistoryUpdatesService } from "../../services/history-updates.service";

function b64toFile(base64: string, name: string, type = "image/jpeg"): File {
  const byteString = atob(base64);
  const ab = new ArrayBuffer(byteString.length);
  const ia = new Uint8Array(ab);
  for (let i = 0; i < byteString.length; i++) ia[i] = byteString.charCodeAt(i);
  const blob = new Blob([ab], { type });
  return new File([blob], name, { type });
}

type ClarifyPreviewInfo = {
  mealId: number;
  note?: string;
  timeUtc: string;
  queued: boolean;
};

type NextClarifyDraft = {
  note?: string;
  time?: string;
};

type ZoomMediaTrackConstraints = MediaTrackConstraints & {
  zoom?: ConstrainDoubleRange;
};

type ZoomMediaTrackConstraintSet = MediaTrackConstraintSet & {
  zoom?: number;
};

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
export class AddMealPage implements OnInit, AfterViewInit, OnDestroy {
  photoDataUrl?: string;    // превью для UI (через dataUrl)
  previewActive = false;
  uploadProgress: number | null = null;
  progressMode: "determinate" | "indeterminate" = "determinate";
  clarifyLoading = false;

  private previewStarting = false;
  private updatesSub?: Subscription;
  private lastMeal?: MealListItem;
  private lastMealDetails?: MealDetails;
  private lastClarifyNote?: string;
  private pendingUpload = false;
  private previousMealId?: number;
  private pendingClarifyResult?: NextClarifyDraft;
  private mediaStream?: MediaStream; // <— DOM-превью поток

  clarifyPreview?: ClarifyPreviewInfo;
  nextClarifyDraft?: NextClarifyDraft;

  @ViewChild("previewBox") private previewBox?: ElementRef<HTMLDivElement>;
  @ViewChild("videoEl") private videoEl?: ElementRef<HTMLVideoElement>;

  constructor(
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private dialog: MatDialog,
    private updates: HistoryUpdatesService
  ) {}

  ngOnInit() {
    void this.refreshLatestMeal();
    this.updatesSub = this.updates.updates().subscribe(item => this.applyUpdate(item));
  }

  async ngAfterViewInit() {
    await this.startPreviewWithFallback();
  }

  async ngOnDestroy() {
    this.updatesSub?.unsubscribe();
    await this.stopPreview();
  }

    async uploadBase64(b64: string) {
    // превью
    this.photoDataUrl = "data:image/jpeg;base64," + b64;
    this.pendingUpload = true;
    if (this.lastMeal) {
      this.previousMealId = this.lastMeal.id;
    }
    this.lastMeal = undefined;
    this.lastMealDetails = undefined;
    this.lastClarifyNote = undefined;
    this.clarifyPreview = undefined;
    // upload
    const file = b64toFile(b64, `meal_${Date.now()}.jpg`);
    this.uploadProgress = 0;
    this.progressMode = "determinate";
    const previousPendingClarify = this.pendingClarifyResult;
    const clarifyDraft = this.nextClarifyDraft;
    const noteToSend = clarifyDraft?.note;
    const timeToSend = clarifyDraft?.time;
    this.pendingClarifyResult = noteToSend || timeToSend ? { note: noteToSend, time: timeToSend } : undefined;
    this.nextClarifyDraft = undefined;

    this.api.uploadPhoto(file, noteToSend, timeToSend).subscribe({
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
        this.pendingUpload = false;
        void this.refreshLatestMeal();
        if (clarifyDraft) {
          this.nextClarifyDraft = clarifyDraft;
          this.pendingClarifyResult = { note: noteToSend, time: timeToSend };
        } else {
          this.pendingClarifyResult = previousPendingClarify;
        }
        this.snack.open("Не удалось отправить фото", "OK", { duration: 2000 });
      }
    });
  }
  // системная камера (Capacitor Camera)
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

  openNextClarifyDialog() {
    const dialogRef = this.dialog.open(VoiceNoteDialogComponent, {
      width: "min(480px, 90vw)",
      maxWidth: "90vw",
      autoFocus: false,
      restoreFocus: false,
      data: {
        title: "Уточнение к следующему фото",
        kind: "photoClarify" as const,
        note: this.nextClarifyDraft?.note,
        time: this.nextClarifyDraft?.time
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (!result || typeof result === "boolean") return;
      const trimmed = result.note?.trim();
      const note = trimmed ? trimmed : undefined;
      const time = result.time ? result.time : undefined;
      if (!note && !time) {
        this.nextClarifyDraft = undefined;
        return;
      }
      this.nextClarifyDraft = { note, time };
    });
  }

  private handleClarifyDialogClose(
    result: (ClarifyResult & { note?: string; createdAtUtc: string }) | { deleted: true } | { queued: true; note?: string; time?: string } | undefined,
    mealId: number
  ) {
    if (!result) return;

    if ("deleted" in result && result.deleted) {
      if (this.lastMeal?.id === mealId) {
        this.lastMeal = undefined;
        this.lastMealDetails = undefined;
        this.lastClarifyNote = undefined;
        this.previousMealId = undefined;
      }
      this.pendingUpload = false;
      if (this.clarifyPreview?.mealId === mealId) {
        this.clarifyPreview = undefined;
      }
      this.snack.open("Запись удалена", "OK", { duration: 1500 });
      void this.refreshLatestMeal();
      return;
    }

    if ("queued" in result && result.queued) {
      const queueResult = result as { queued: true; note?: string; time?: string };
      if (this.lastMeal?.id === mealId) {
        this.lastMeal = { ...this.lastMeal, updateQueued: true };
      }
      const trimmedNoteRaw = queueResult.note?.trim();
      this.lastClarifyNote = trimmedNoteRaw ? trimmedNoteRaw : undefined;
      if (this.lastMealDetails?.id === mealId) {
        const clarifyNoteValue = trimmedNoteRaw !== undefined
          ? (trimmedNoteRaw ? trimmedNoteRaw : null)
          : this.lastMealDetails.clarifyNote;
        const createdAtUtc = queueResult.time ?? this.lastMealDetails.createdAtUtc;
        this.lastMealDetails = {
          ...this.lastMealDetails,
          clarifyNote: clarifyNoteValue,
          createdAtUtc
        };
      }
      this.setClarifyPreview(this.lastClarifyNote, mealId, queueResult.time, true);
      this.snack.open("Уточнение отправлено", "OK", { duration: 1500 });
      return;
    }

    const res = result as ClarifyResult & { note?: string; createdAtUtc: string };
    const resultNote = res.note?.trim();
    const noteToStore = resultNote ?? this.lastClarifyNote;
    this.lastClarifyNote = noteToStore ? noteToStore : undefined;
    if (this.lastMealDetails?.id === mealId) {
      this.lastMealDetails = {
        ...this.lastMealDetails,
        clarifyNote: this.lastClarifyNote ?? null,
        createdAtUtc: res.createdAtUtc
      };
    }
    const item: MealListItem = {
      id: res.id,
      createdAtUtc: res.createdAtUtc,
      dishName: res.result.dish,
      weightG: res.result.weight_g,
      caloriesKcal: res.result.calories_kcal,
      proteinsG: res.result.proteins_g,
      fatsG: res.result.fats_g,
      carbsG: res.result.carbs_g,
      ingredients: res.result.ingredients,
      products: res.products,
      hasImage: this.lastMeal?.hasImage ?? this.lastMealDetails?.hasImage ?? true,
      updateQueued: false
    };
    this.setClarifyPreview(this.lastClarifyNote, mealId, res.createdAtUtc, false);
    this.updateLastMeal(item);
    this.snack.open("Уточнение применено", "OK", { duration: 1500 });
  }

  private async ensureLatestMealForClarify(): Promise<MealListItem | undefined> {
    if (this.lastMeal) return this.lastMeal;
    await this.refreshLatestMeal();
    return this.lastMeal;
  }

  private async refreshLatestMeal() {
    try {
      const res = await firstValueFrom(this.api.getMeals(1, 0));
      const item = res.items?.[0];
      if (!item) {
        if (!this.pendingUpload) {
          this.lastMeal = undefined;
          this.previousMealId = undefined;
          this.lastClarifyNote = undefined;
          this.lastMealDetails = undefined;
          this.clarifyPreview = undefined;
        }
        return;
      }
      if (this.pendingUpload && this.previousMealId && item.id === this.previousMealId) {
        return;
      }
      this.updateLastMeal(item);
    } catch (err) {
      console.error(err);
    }
  }

  private applyUpdate(item: MealListItem) {
    if (this.pendingUpload) {
      if (!this.previousMealId || item.id !== this.previousMealId) {
        this.updateLastMeal(item);
      }
      return;
    }

    if (!this.lastMeal || this.lastMeal.id === item.id) {
      this.updateLastMeal(item);
      return;
    }

    const lastTime = Date.parse(this.lastMeal.createdAtUtc);
    const newTime = Date.parse(item.createdAtUtc);
    if (Number.isNaN(lastTime) || Number.isNaN(newTime) || newTime >= lastTime) {
      this.updateLastMeal(item);
    }
  }

  private updateLastMeal(item: MealListItem) {
    this.lastMeal = item;
    this.previousMealId = item.id;
    this.pendingUpload = false;
    this.lastMealDetails = undefined;
    const pendingClarify = this.pendingClarifyResult;
    if (pendingClarify) {
      const noteValue = pendingClarify.note;
      const timeValue = pendingClarify.time ?? item.createdAtUtc;
      this.lastClarifyNote = noteValue ? noteValue : undefined;
      this.setClarifyPreview(this.lastClarifyNote, item.id, timeValue, false);
      this.pendingClarifyResult = undefined;
      return;
    }
    if (this.clarifyPreview?.mealId !== item.id) {
      this.clarifyPreview = undefined;
      this.lastClarifyNote = undefined;
    } else {
      this.clarifyPreview = {
        ...this.clarifyPreview,
        timeUtc: item.createdAtUtc,
        queued: item.updateQueued
      };
    }
  }

  private setClarifyPreview(note: string | undefined, mealId: number, timeUtc?: string, queued = false) {
    const trimmed = note?.trim();
    const baseTime = timeUtc ?? this.lastMealDetails?.createdAtUtc ?? this.lastMeal?.createdAtUtc ?? this.clarifyPreview?.timeUtc;
    if (!baseTime) {
      if (this.clarifyPreview?.mealId === mealId) {
        const preview: ClarifyPreviewInfo = {
          mealId,
          timeUtc: this.clarifyPreview.timeUtc,
          queued
        };
        if (trimmed) preview.note = trimmed;
        this.clarifyPreview = preview;
      }
      return;
    }
    const preview: ClarifyPreviewInfo = {
      mealId,
      timeUtc: baseTime,
      queued
    };
    if (trimmed) preview.note = trimmed;
    this.clarifyPreview = preview;
  }

  retryPreview() {
    void this.startPreviewWithFallback();
  }

  // === DOM preview (WebRTC) ===
  private async startDomPreview() {
    if (this.previewActive || this.previewStarting) return;
    this.previewStarting = true;

    // Базовые ограничения — задняя камера и без звука
    const videoConstraints: ZoomMediaTrackConstraints = {
      facingMode: { ideal: "environment" },
      zoom: { ideal: 1 }
    };
    const base: MediaStreamConstraints = {
      video: videoConstraints,
      audio: false
    };

    try {
      let stream: MediaStream | null = null;
      try {
        stream = await navigator.mediaDevices.getUserMedia(base);
      } catch {
        // Фолбэк: без facingMode (некоторые WebView/устройства)
        stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
      }

      this.mediaStream = stream!;
      const [track] = stream.getVideoTracks();
      if (track && "getCapabilities" in track) {
        const capabilities = track.getCapabilities() as MediaTrackCapabilities & {
          zoom?: { min?: number; max?: number };
        };
        const zoomCap = capabilities.zoom;
        if (
          zoomCap &&
          typeof zoomCap.min === "number" &&
          typeof zoomCap.max === "number" &&
          zoomCap.min <= 1 &&
          zoomCap.max >= 1
        ) {
          try {
            const zoomConstraint: ZoomMediaTrackConstraintSet = { zoom: 1 };
            const zoomApplyConstraints: MediaTrackConstraints = { advanced: [zoomConstraint] };
            await track.applyConstraints(zoomApplyConstraints);
          } catch {
            // ignore zoom errors on devices without support
          }
        }
      }
      const video = this.videoEl?.nativeElement;
      if (video) {
        video.srcObject = stream!;
        await video.play();
        // Установим корректную aspect-ratio после получения метаданных
        const onMeta = () => {
          const w = video.videoWidth || 3;
          const h = video.videoHeight || 4;
          this.previewBox?.nativeElement?.style.setProperty("--camera-aspect", `${w} / ${h}`);
          video.removeEventListener('loadedmetadata', onMeta);
        };
        video.addEventListener('loadedmetadata', onMeta);
      }

      this.previewActive = true;
    } finally {
      this.previewStarting = false;
    }
  }

  private async startPreviewWithFallback() {
    const hasPermission = await this.ensureCameraPermission();
    if (!hasPermission) {
      this.previewActive = false;
      this.snack.open("Доступ к камере не предоставлен. Откроется системная камера.", "OK", { duration: 2500 });
      await this.takePhotoSystem();
      return;
    }

    try {
      await this.startDomPreview();
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
    try {
      const video = this.videoEl?.nativeElement;
      if (video) {
        video.pause();
        
        video.srcObject = null;
      }
      if (this.mediaStream) {
        this.mediaStream.getTracks().forEach(t => t.stop());
        this.mediaStream = undefined;
      }
    } finally {
      this.previewActive = false;
      this.previewStarting = false;
    }
  }

  async captureFromPreview() {
    const video = this.videoEl?.nativeElement;
    if (!video || !this.previewActive || !this.mediaStream) {
      await this.takePhotoSystem();
      return;
    }

    try {
      // Создать канвас под реальное разрешение видеопотока
      const w = video.videoWidth;
      const h = video.videoHeight;
      if (!w || !h) throw new Error('Video metadata not ready');

      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      const ctx = canvas.getContext('2d');
      if (!ctx) throw new Error('Canvas context failed');
      ctx.drawImage(video, 0, 0, w, h);
      const dataUrl = canvas.toDataURL('image/jpeg', 0.95);
      const b64 = dataUrl.replace('data:image/jpeg;base64,', '');
      await this.uploadBase64(b64);
    } catch (err) {
      this.handleCameraError(err, "Не удалось сделать фото");
    }
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

  private async ensureCameraPermission(): Promise<boolean> {
    try {
      const current = await Camera.checkPermissions();
      if (this.isCameraPermissionGranted(current.camera)) {
        return true;
      }
      const requested = await Camera.requestPermissions({ permissions: ["camera"] as CameraPermissionType[] });
      return this.isCameraPermissionGranted(requested.camera);
    } catch (err) {
      console.error("Failed to obtain camera permission", err);
      return false;
    }
  }

  private isCameraPermissionGranted(state: CameraPermissionState | undefined): boolean {
    return state === "granted" || state === "limited";
  }
}