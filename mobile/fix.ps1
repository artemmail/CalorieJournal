# Патч для приведения standalone-проекта к NgModule и фикса TS2729
param()

function Write-Text($Path, $Content) {
  $dir = Split-Path -Parent $Path
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8 -Force
  Write-Host "Wrote: $Path"
}

# 1) main.ts -> bootstrap AppModule
Write-Text "src/main.ts" @'
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { AppModule } from './app/app.module';
import 'zone.js';

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.error(err));
'@

# 2) Перезаписываем app.component.ts на не-standalone
Write-Text "src/app/app.component.ts" @'
import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent {}
'@

# 3) Удаляем standalone: true и imports: [...] из ВСЕХ компонентов/пайпов в src/app
Get-ChildItem -Recurse -Include *.ts -Path "src/app" | ForEach-Object {
  $p = $_.FullName
  $c = Get-Content -LiteralPath $p -Raw
  $c2 = [regex]::Replace($c, 'standalone\s*:\s*true\s*,?', '', 'IgnoreCase')
  $c2 = [regex]::Replace($c2, 'imports\s*:\s*\[[\s\S]*?\]\s*,?', '', 'IgnoreCase')
  if ($c2 -ne $c) {
    Set-Content -LiteralPath $p -Value $c2 -Encoding UTF8
    Write-Host "Patched decorator: $p"
  }
}

# 4) Чиним TS2729 (инициализация форм в конструкторе)
# meal-dialog.component.ts
Write-Text "src/app/components/meal-dialog/meal-dialog.component.ts" @'
import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, Validators, FormGroup } from '@angular/forms';
import { VoiceService } from '../../services/voice.service';

@Component({
  selector: 'app-meal-dialog',
  templateUrl: './meal-dialog.component.html',
  styleUrls: ['./meal-dialog.component.scss']
})
export class MealDialogComponent {
  loadingVoice = false;
  form!: FormGroup;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { meal: any },
    private ref: MatDialogRef<MealDialogComponent>,
    private fb: FormBuilder,
    private voice: VoiceService
  ) {
    this.form = this.fb.group({
      calories: [data.meal?.calories ?? 0, [Validators.required, Validators.min(0)]],
      proteins: [data.meal?.proteins ?? 0, [Validators.min(0)]],
      fats: [data.meal?.fats ?? 0, [Validators.min(0)]],
      carbs: [data.meal?.carbs ?? 0, [Validators.min(0)]],
      note: [data.meal?.note ?? '']
    });
  }

  async speakFill() {
    this.loadingVoice = true;
    try {
      const text = await this.voice.listenOnce('ru-RU');
      const parsed = this.voice.parseMacros(text);
      this.form.patchValue(parsed);
    } catch {
      alert('Не удалось распознать речь');
    } finally {
      this.loadingVoice = false;
    }
  }

  save() {
    if (this.form.invalid) return;
    this.ref.close(this.form.value);
  }
}
'@

# add-meal.page.ts
Write-Text "src/app/pages/add-meal/add-meal.page.ts" @'
import { Component } from '@angular/core';
import { FormBuilder, Validators, FormGroup } from '@angular/forms';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { MealService } from '../../services/meal.service';
import { StorageService } from '../../services/storage.service';

@Component({
  selector: 'app-add-meal',
  templateUrl: './add-meal.page.html',
  styleUrls: ['./add-meal.page.scss']
})
export class AddMealPage {
  photoUri: string | undefined;
  form!: FormGroup;

  constructor(private fb: FormBuilder, private meals: MealService, private storage: StorageService) {
    this.form = this.fb.group({
      calories: [null, [Validators.required, Validators.min(0)]],
      proteins: [0, [Validators.min(0)]],
      fats: [0, [Validators.min(0)]],
      carbs: [0, [Validators.min(0)]],
      note: ['']
    });
  }

  async takePhoto() {
    const img = await Camera.getPhoto({
      quality: 70,
      resultType: CameraResultType.Base64,
      source: CameraSource.Camera
    });
    if (img.base64String) {
      this.photoUri = await this.storage.saveImageBase64ToDataDir(img.base64String);
    }
  }

  save() {
    if (this.form.invalid) return;
    const v = this.form.value as any;
    this.meals.add({
      timestamp: Date.now(),
      photoUri: this.photoUri,
      calories: Number(v.calories),
      proteins: Number(v.proteins),
      fats: Number(v.fats),
      carbs: Number(v.carbs),
      note: v.note || undefined
    });
    this.form.reset();
    this.photoUri = undefined;
    alert('Сохранено');
  }
}
'@

Write-Host "`nPatch complete. Try:"
Write-Host "  ng build"
