import { Component, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";

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
    MatCardModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSnackBarModule
  ],
  templateUrl: "./add-meal.page.html",
  styleUrls: ["./add-meal.page.scss"]
})
export class AddMealPage implements OnDestroy {
  photoDataUrl?: string;    // превью для UI (через pipe convert)
  form!: FormGroup;
  previewActive = false;

  lastResult: any;

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
    if (r?.value) await this.uploadBase64(r.value);
    await this.stopPreview();
  }

  async uploadBase64(b64: string) {
    // превью
    this.photoDataUrl = "data:image/jpeg;base64," + b64;
    // upload
    const file = b64toFile(b64, `meal_${Date.now()}.jpg`);
    this.api.uploadPhoto(file).subscribe({
      next: (res) => {
        this.lastResult = res;
        this.snack.open("Фото отправлено. Расчёт готов.", "OK", { duration: 1500 });
      },
      error: () => this.snack.open("Не удалось отправить фото", "OK", { duration: 2000 })
    });
  }

  async ngOnDestroy() { await this.stopPreview(); }
}

