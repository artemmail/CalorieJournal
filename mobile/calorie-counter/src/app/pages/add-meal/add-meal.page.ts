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
  private availableCameras: MediaDeviceInfo[] = [];
  private currentCameraIndex = -1;
  private currentCameraId?: string;

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
    const hadDomPreview = this.previewActive || this.previewStarting || !!this.mediaStream;
    if (hadDomPreview) {
      await this.stopPreview();
    }
    try {
      const img = await Camera.getPhoto({ quality: 80, resultType: CameraResultType.Base64, source: CameraSource.Camera });
      if (img.base64String) {
        await this.uploadBase64(img.base64String);
      }
    } catch (err) {
      this.handleCameraError(err, "Не удалось сделать фото");
    } finally {
      if (hadDomPreview) {
        await this.startPreviewWithFallback();
      }
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
  private setPreviewAspect(width?: number | null, height?: number | null, ratio?: number | null) {
    const host = this.previewBox?.nativeElement;
    if (!host) return;

    const hasWidth = typeof width === "number" && width > 0;
    const hasHeight = typeof height === "number" && height > 0;

    if (hasWidth && hasHeight) {
      host.style.setProperty("--camera-aspect", `${width} / ${height}`);
      return;
    }

    if (typeof ratio === "number" && Number.isFinite(ratio) && ratio > 0) {
      host.style.setProperty("--camera-aspect", `${ratio}`);
    }
  }

  private async startDomPreview(preferredDeviceId?: string): Promise<MediaStreamTrack | undefined> {
    if (this.previewStarting) return;

    this.previewStarting = true;

    if (this.previewActive) {
      await this.stopPreview();
      this.previewStarting = true;
    }

    // Базовые ограничения — задняя камера и без звука
    const videoConstraints: ZoomMediaTrackConstraints = 
    


    
    
    preferredDeviceId
      ? { deviceId: { exact: preferredDeviceId }, zoom: { ideal: 1 } }
      :     
    {
    facingMode: { ideal: "environment" },
    width: { ideal: 1920 },
    height: { ideal: 1080 },
    aspectRatio: { ideal: 16/9 },
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
        if (preferredDeviceId) {
          stream = await navigator.mediaDevices.getUserMedia({
            video: { deviceId: { exact: preferredDeviceId } },
            audio: false
          });
        } else {
          stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        }
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
        const onMeta = () => {
          this.setPreviewAspect(video.videoWidth || null, video.videoHeight || null);
          video.removeEventListener("loadedmetadata", onMeta);
        };
        video.addEventListener("loadedmetadata", onMeta);

        if (track) {
          const settings = track.getSettings();
          this.setPreviewAspect(
            settings.width ?? null,
            settings.height ?? null,
            settings.aspectRatio ?? null
          );
        }

        video.srcObject = stream!;
        await video.play();
        if (video.readyState >= HTMLMediaElement.HAVE_METADATA) {
          onMeta();
        }
      }

      if (track) {
        const settings = track.getSettings();
        const deviceId = settings.deviceId ?? preferredDeviceId;
        if (deviceId) {
          this.currentCameraId = deviceId;
          this.updateCurrentCameraIndex(deviceId);
        }
      }

      this.previewActive = true;
      void this.loadAvailableCameras();
      return track;
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
      void this.alertBackCameras();
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

  async switchCamera() {
    if (this.previewStarting) return;

    const hasPermission = await this.ensureCameraPermission();
    if (!hasPermission) {
      this.snack.open("Доступ к камере не предоставлен", "OK", { duration: 2000 });
      return;
    }

    const cameras = await this.loadAvailableCameras();
    if (!cameras.length) {
      //alert("Камеры недоступны");
      return;
    }

    const nextIndex = this.getNextCameraIndex(cameras.length);
    const nextCamera = cameras[nextIndex];

    try {
      const track = await this.startDomPreview(nextCamera.deviceId);
      if (track) {
        await this.alertCameraDetails(nextCamera, track);
      } else {
        //alert(`Камера: ${nextCamera.label || "Неизвестная камера"}`);
      }
    } catch (err) {
      console.error("Failed to switch camera", err);
      //alert("Не удалось переключить камеру");
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

  private async alertBackCameras() {
    if (!navigator?.mediaDevices?.enumerateDevices) {
      //alert("Браузер не поддерживает перечисление камер");
      return;
    }

    try {
      const devices = await this.loadAvailableCameras();
      const rearCameras = devices.filter(device =>
        device.kind === "videoinput" && this.isBackCameraLabel(device.label)
      );

      if (!rearCameras.length) {
        //alert("Задние камеры не найдены");
        return;
      }

      const listParts: string[] = [];
      for (const [index, device] of rearCameras.entries()) {
        const zoomInfo = await this.getCameraZoomDescription(device.deviceId);
        const label = device.label || "Неизвестная камера";
        listParts.push(`${index + 1}. ${label}${zoomInfo ? ` (${zoomInfo})` : ""}`);
      }
      const list = listParts.join("\n");
      //alert(`Доступные задние камеры:\n${list}`);
    } catch (err) {
      console.error("Failed to enumerate camera devices", err);
      //alert("Не удалось получить список камер");
    }
  }

  private isBackCameraLabel(label: string): boolean {
    if (!label) return false;
    const normalized = label.toLowerCase();
    return ["back", "rear", "environment", "world"].some(token => normalized.includes(token));
  }

  private async getCameraZoomDescription(deviceId: string | undefined): Promise<string | null> {
    if (!deviceId) return null;

    const currentTrack = this.mediaStream?.getVideoTracks()?.[0];
    const currentDeviceId = currentTrack?.getSettings()?.deviceId;
    if (currentTrack && currentDeviceId === deviceId) {
      const currentZoom = this.describeZoomFromTrack(currentTrack);
      if (currentZoom) return currentZoom;
    }

    return await this.probeCameraZoom(deviceId);
  }

  private async loadAvailableCameras(): Promise<MediaDeviceInfo[]> {
    if (!navigator?.mediaDevices?.enumerateDevices) {
      this.availableCameras = [];
      this.currentCameraIndex = -1;
      return [];
    }

    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      const videoInputs = devices.filter(device => device.kind === "videoinput");
      videoInputs.sort((a, b) => this.cameraPriority(b) - this.cameraPriority(a));
      this.availableCameras = videoInputs;
      this.updateCurrentCameraIndex(this.currentCameraId);
      return videoInputs;
    } catch (err) {
      console.error("Failed to enumerate camera devices", err);
      return this.availableCameras;
    }
  }

  private cameraPriority(device: MediaDeviceInfo): number {
    return this.isBackCameraLabel(device.label) ? 2 : 1;
  }

  private updateCurrentCameraIndex(deviceId: string | undefined) {
    if (!deviceId) {
      this.currentCameraIndex = -1;
      return;
    }
    const index = this.availableCameras.findIndex(device => device.deviceId === deviceId);
    this.currentCameraIndex = index;
  }

  private getNextCameraIndex(total: number): number {
    if (!total) return 0;
    if (this.currentCameraIndex < 0 || this.currentCameraIndex >= total) {
      return 0;
    }
    return (this.currentCameraIndex + 1) % total;
  }

  private async alertCameraDetails(device: MediaDeviceInfo, track: MediaStreamTrack) {
    const label = device.label || "Неизвестная камера";
    const lines: string[] = [`Камера: ${label}`];

    const settings = track.getSettings();
    const facing = this.extractFacingMode(settings.facingMode);
    if (facing) {
      lines.push(`Расположение: ${facing}`);
    }

    if (typeof settings.width === "number" && typeof settings.height === "number") {
      lines.push(`Разрешение: ${settings.width}×${settings.height}`);
    }

    if (typeof settings.frameRate === "number") {
      lines.push(`Частота кадров: ${this.formatFrameRate(settings.frameRate)} fps`);
    }

    const zoomRaw = this.describeZoomFromTrack(track) ?? await this.getCameraZoomDescription(device.deviceId);
    if (zoomRaw) {
      lines.push(`Зум: ${this.normalizeZoomText(zoomRaw)}`);
    }

   lines.push(JSON.stringify(settings.zoom));

   lines.push(JSON.stringify(settings));

    if (lines.length === 1) {
      lines.push("Дополнительные характеристики недоступны");
    }

    //alert(lines.join("\n"));
  }

  private describeZoomFromTrack(track: MediaStreamTrack): string | null {
    if (!("getCapabilities" in track)) return null;
    const capabilities = track.getCapabilities() as MediaTrackCapabilities & {
      zoom?: { min?: number; max?: number };
    };
    return this.describeZoomCapability(capabilities.zoom);
  }

  private async probeCameraZoom(deviceId: string): Promise<string | null> {
    if (!navigator?.mediaDevices?.getUserMedia) return null;

    let stream: MediaStream | undefined;
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        video: { deviceId: { exact: deviceId } },
        audio: false
      });
      const [track] = stream.getVideoTracks();
      if (!track) return null;
      return this.describeZoomFromTrack(track);
    } catch (err) {
      console.warn("Failed to probe zoom for camera", deviceId, err);
      return null;
    } finally {
      stream?.getTracks().forEach(track => track.stop());
    }
  }

  private describeZoomCapability(zoomCap: { min?: number; max?: number } | undefined): string | null {
    if (!zoomCap || typeof zoomCap.max !== "number") return null;

    const max = zoomCap.max;
    if (!Number.isFinite(max) || max <= 1) return null;

    const minRaw = typeof zoomCap.min === "number" ? zoomCap.min : undefined;
    const min = minRaw && Number.isFinite(minRaw) ? Math.max(1, minRaw) : 1;

    const maxText = this.formatZoomValue(max);
    if (Math.abs(min - max) < 0.05) {
      return `zoom ${maxText}x`;
    }

    if (min <= 1.05) {
      return `zoom до ${maxText}x`;
    }

    const minText = this.formatZoomValue(min);
    return `zoom ${minText}–${maxText}x`;
  }

  private formatZoomValue(value: number): string {
    const rounded = Math.round(value * 10) / 10;
    return Number.isInteger(rounded) ? rounded.toFixed(0) : rounded.toFixed(1);
  }

  private extractFacingMode(mode: string | string[] | undefined): string | null {
    if (!mode) return null;
    const value = Array.isArray(mode) ? mode[0] : mode;
    switch (value) {
      case "environment":
        return "тыловая";
      case "user":
        return "фронтальная";
      case "left":
        return "левая";
      case "right":
        return "правая";
      default:
        return value;
    }
  }

  private formatFrameRate(value: number): string {
    const rounded = Math.round(value * 10) / 10;
    return Number.isInteger(rounded) ? rounded.toFixed(0) : rounded.toFixed(1);
  }

  private normalizeZoomText(raw: string): string {
    const cleaned = raw.replace(/^zoom\s*/i, "").trim();
    return cleaned || raw;
  }
}
