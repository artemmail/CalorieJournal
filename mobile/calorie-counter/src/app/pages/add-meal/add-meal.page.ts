import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from "@angular/core";
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
  photoDataUrl?: string;    // превью для UI (через pipe convert)
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
  clarifyPreview?: ClarifyPreviewInfo;
  nextClarifyDraft?: NextClarifyDraft;

  @ViewChild("previewBox")
  private previewBox?: ElementRef<HTMLDivElement>;

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

  // превью
  private async startPreview() {
    if (this.previewActive || this.previewStarting) return;
    this.previewStarting = true;
    const { cssWidth, cssHeight } = this.resolvePreviewSize();
    const opts: CameraPreviewOptions = {
      parent: "cameraPreview",
      className: "cameraPreview",
      position: "rear",
      disableAudio: true,
      toBack: false,
      width: cssWidth,
      height: cssHeight,
      disableExifHeaderStripping: false,
      enableHighResolution: true
    };
    try {
      await CameraPreview.start(opts);
      this.previewActive = true;
    } finally {
      this.previewStarting = false;
    }
  }

  private resolvePreviewSize1() {
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

  private resolvePreviewSize() {
    const el = this.previewBox?.nativeElement;
    const container = el?.parentElement as HTMLElement | undefined;
    const cssWidth = Math.max(1, Math.round(container?.clientWidth || window.innerWidth));

    const ratio = this.resolveDeviceAspectRatio();
    this.updatePreviewAspect(container, ratio);

    const heightCss = Math.max(1, Math.round(cssWidth / ratio));

    const scale = window.devicePixelRatio || 1;
    const captureWidth = Math.max(1, Math.round(cssWidth * scale));
    const captureHeight = Math.max(1, Math.round(heightCss * scale));

    return {
      cssWidth,
      cssHeight: heightCss,
      captureWidth,
      captureHeight
    };
  }

  private resolveDeviceAspectRatio() {
    const screen = window.screen;
    if (screen?.width && screen?.height) {
      const min = Math.min(screen.width, screen.height);
      const max = Math.max(screen.width, screen.height);
      const ratio = min / max;
      if (ratio > 0.4 && ratio < 1.1) {
        return ratio;
      }
    }
    return 3 / 4;
  }

  private updatePreviewAspect(container: HTMLElement | undefined, ratio: number) {
    container?.style.setProperty("--camera-aspect", ratio.toString());
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
    this.resolvePreviewSize();
    const picOpts: CameraPreviewPictureOptions = {
      quality: 90
    };
    try {
      const r = await CameraPreview.capture(picOpts); // { value: base64 }
      if (r?.value) {
        const normalized = await this.normalizeCapturedImage(r.value);
        await this.uploadBase64(normalized);
      }
    } catch (err) {
      this.handleCameraError(err, "Не удалось сделать фото");
    }
  }

  private async normalizeCapturedImage(b64: string): Promise<string> {
    if (!b64) return b64;
    if (window.innerWidth >= window.innerHeight) return b64;

    return new Promise((resolve) => {
      const img = new Image();
      img.onload = () => {
        if (img.naturalHeight >= img.naturalWidth) {
          resolve(b64);
          return;
        }
        const canvas = document.createElement("canvas");
        canvas.width = img.naturalHeight;
        canvas.height = img.naturalWidth;
        const ctx = canvas.getContext("2d");
        if (!ctx) {
          resolve(b64);
          return;
        }
        ctx.translate(canvas.width / 2, canvas.height / 2);
        ctx.rotate(-Math.PI / 2);
        ctx.drawImage(img, -img.naturalWidth / 2, -img.naturalHeight / 2);
        const rotated = canvas
          .toDataURL("image/jpeg", 0.95)
          .replace("data:image/jpeg;base64,", "");
        resolve(rotated);
      };
      img.onerror = () => resolve(b64);
      img.src = "data:image/jpeg;base64," + b64;
    });
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
