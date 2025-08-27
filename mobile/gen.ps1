param(
  [string]$ProjectName = "calorie-counter",
  [string]$AppName     = "Calorie Counter",
  [string]$AppId       = "com.yourscriptor.calorie"
)

# --- helpers ---
function Write-Text($Path, $Content) {
  $dir = Split-Path -Parent $Path
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8 -Force
  Write-Host "Wrote: $Path"
}

function Append-Unique($Path, $Lines) {
  if (-not (Test-Path $Path)) { Set-Content -LiteralPath $Path -Value $Lines -Encoding UTF8; return }
  $current = Get-Content -LiteralPath $Path -Raw
  if ($current -notmatch [regex]::Escape($Lines)) {
    Add-Content -LiteralPath $Path -Value $Lines -Encoding UTF8
    Write-Host "Appended to: $Path"
  }
}

function Ensure-AngularCLI {
  try { ng version | Out-Null }
  catch {
    Write-Host "Installing @angular/cli globally..."
    npm i -g @angular/cli
  }
}

function Add-AndroidPermission($ManifestPath, $PermissionLine) {
  if (-not (Test-Path $ManifestPath)) { return }
  $xml = Get-Content -LiteralPath $ManifestPath -Raw
  if ($xml -notmatch [regex]::Escape($PermissionLine)) {
    $xml = $xml -replace '(<manifest[^>]*>)', "`$1`n  $PermissionLine"
    Set-Content -LiteralPath $ManifestPath -Value $xml -Encoding UTF8
    Write-Host "Added permission: $PermissionLine"
  }
}

# --- 0) create Angular project ---
Ensure-AngularCLI

Write-Host "Creating Angular project $ProjectName ..."
# --no-standalone, чтобы использовать NgModule структуру
ng new $ProjectName --routing --style=scss --no-standalone --skip-git --package-manager npm

if (-not (Test-Path $ProjectName)) { throw "Angular project was not created." }
Set-Location $ProjectName

# --- 1) Angular Material ---
try {
  ng add @angular/material --skip-confirmation --defaults
} catch {
  Write-Host "ng add @angular/material failed, installing packages manually..."
  npm i @angular/material @angular/cdk @angular/animations
  # гарантируем BrowserAnimationsModule в app.module.ts позже
}

# --- 2) Capacitor + plugins ---
npm i @capacitor/core @capacitor/android @capacitor/camera @capacitor/preferences @capacitor/filesystem
npm i @capacitor-community/speech-recognition

# --- 3) Capacitor init & Android ---
npx cap init "$AppName" "$AppId" --web-dir "dist/$ProjectName" --npm-client npm
npx cap add android

# --- 4) write source files ---

# capacitor.config.ts
Write-Text "capacitor.config.ts" @'
import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourscriptor.calorie',
  appName: 'Calorie Counter',
  webDir: 'dist/calorie-counter',
  bundledWebRuntime: false,
  android: { allowMixedContent: true }
};

export default config;
'@

# MaterialModule
Write-Text "src/app/shared/material.module.ts" @'
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';

@NgModule({
  imports: [CommonModule],
  exports: [
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatCardModule,
    MatListModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule,
    MatMenuModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSelectModule,
    MatTabsModule,
    MatChipsModule,
    MatDividerModule
  ]
})
export class MaterialModule {}
'@

# models/meal.ts
Write-Text "src/app/models/meal.ts" @'
export interface Meal {
  id: string;
  timestamp: number;
  photoUri?: string;
  calories: number;
  proteins: number;
  fats: number;
  carbs: number;
  note?: string;
}
'@

# services/storage.service.ts
Write-Text "src/app/services/storage.service.ts" @'
import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';
import { Filesystem, Directory } from '@capacitor/filesystem';

const MEALS_KEY = 'meals_v1';

@Injectable({ providedIn: 'root' })
export class StorageService {
  async loadMeals(): Promise<any[]> {
    const { value } = await Preferences.get({ key: MEALS_KEY });
    if (!value) return [];
    try { return JSON.parse(value); } catch { return []; }
  }

  async saveMeals(meals: any[]): Promise<void> {
    await Preferences.set({ key: MEALS_KEY, value: JSON.stringify(meals) });
  }

  async saveImageBase64ToDataDir(base64: string): Promise<string> {
    const fileName = `meal_${Date.now()}.jpeg`;
    await Filesystem.writeFile({ path: fileName, data: base64, directory: Directory.Data });
    const uri = await Filesystem.getUri({ path: fileName, directory: Directory.Data });
    return uri.uri;
  }
}
'@

# services/meal.service.ts
Write-Text "src/app/services/meal.service.ts" @'
import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Meal } from '../models/meal';
import { StorageService } from './storage.service';

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Injectable({ providedIn: 'root' })
export class MealService {
  private _meals$ = new BehaviorSubject<Meal[]>([]);
  meals$ = this._meals$.asObservable();

  constructor(private storage: StorageService) {
    this.init();
  }

  async init() {
    const meals = await this.storage.loadMeals();
    this._meals$.next(meals);
  }

  private persist() { this.storage.saveMeals(this._meals$.value); }

  getAll(): Meal[] { return this._meals$.value.sort((a, b) => b.timestamp - a.timestamp); }

  add(meal: Omit<Meal, 'id'>) {
    const m: Meal = { id: uuid(), ...meal };
    const next = [m, ...this._meals$.value];
    this._meals$.next(next);
    this.persist();
  }

  update(id: string, patch: Partial<Meal>) {
    const next = this._meals$.value.map(m => (m.id === id ? { ...m, ...patch } : m));
    this._meals$.next(next);
    this.persist();
  }

  remove(id: string) {
    const next = this._meals$.value.filter(m => m.id !== id);
    this._meals$.next(next);
    this.persist();
  }

  sumForDate(date: Date) {
    const start = new Date(date); start.setHours(0, 0, 0, 0);
    const end = new Date(date); end.setHours(23, 59, 59, 999);
    return this.sumRange(start.getTime(), end.getTime());
  }

  sumDaysBack(days: number) {
    const end = Date.now();
    const start = end - days * 24 * 60 * 60 * 1000;
    return this.sumRange(start, end);
  }

  private sumRange(startMs: number, endMs: number) {
    const meals = this._meals$.value.filter(m => m.timestamp >= startMs && m.timestamp <= endMs);
    const totals = meals.reduce((acc, m) => {
      acc.calories += m.calories || 0;
      acc.proteins += m.proteins || 0;
      acc.fats += m.fats || 0;
      acc.carbs += m.carbs || 0;
      return acc;
    }, { calories: 0, proteins: 0, fats: 0, carbs: 0 });
    return { totals, count: meals.length };
  }
}
'@

# services/voice.service.ts
Write-Text "src/app/services/voice.service.ts" @'
import { Injectable } from '@angular/core';
import { SpeechRecognition } from '@capacitor-community/speech-recognition';

@Injectable({ providedIn: 'root' })
export class VoiceService {
  private listening = false;

  async isAvailable(): Promise<boolean> {
    try {
      const avail = await SpeechRecognition.available();
      return !!avail.available;
    } catch { return false; }
  }

  async ensurePermission() {
    const has = await SpeechRecognition.checkPermission();
    if (has.permission !== 'granted') {
      await SpeechRecognition.requestPermission();
    }
  }

  async listenOnce(lang: string = 'ru-RU'): Promise<string> {
    await this.ensurePermission();
    if (this.listening) await SpeechRecognition.stop();
    this.listening = true;

    return new Promise(async (resolve, reject) => {
      const resultHandler = (data: any) => {
        const text = (data.matches?.[0] || '').toString();
        cleanup();
        resolve(text);
      };
      const errorHandler = (err: any) => { cleanup(); reject(err); };
      const endHandler = () => { /* noop */ };

      const cleanup = () => {
        SpeechRecognition.removeAllListeners();
        this.listening = false;
      };

      try {
        await SpeechRecognition.start({
          language: lang,
          maxResults: 1,
          partialResults: false,
          popup: true
        });
        SpeechRecognition.addListener('result', resultHandler);
        SpeechRecognition.addListener('error', errorHandler);
        SpeechRecognition.addListener('end', endHandler);
      } catch (e) {
        cleanup();
        reject(e);
      }
    });
  }

  parseMacros(text: string): { calories?: number; proteins?: number; fats?: number; carbs?: number } {
    const t = text.toLowerCase().replace(/[,;]/g, ' ');
    const out: any = {};
    const num = (s?: string) => s ? Number(s.replace(/[^\d.]/g, '')) : NaN;

    const kcal = t.match(/(ккал|калори[ияе]|kcal|cal)\s*(\d+[\.,]?\d*)/);
    if (kcal) out.calories = num(kcal[2]);

    const p = t.match(/(б\b|белк\w*)\s*(\d+[\.,]?\d*)/);
    if (p) out.proteins = num(p[2]);

    const f = t.match(/(ж\b|жир\w*)\s*(\d+[\.,]?\d*)/);
    if (f) out.fats = num(f[2]);

    const c = t.match(/(у\b|углев\w*)\s*(\d+[\.,]?\d*)/);
    if (c) out.carbs = num(c[2]);

    if (!out.calories || !out.proteins || !out.fats || !out.carbs) {
      const nums = t.match(/\d+[\.,]?\d*/g)?.map(s => Number(s.replace(',', '.')));
      if (nums && nums.length >= 4) {
        out.calories ??= nums[0];
        out.proteins ??= nums[1];
        out.fats ??= nums[2];
        out.carbs ??= nums[3];
      }
    }
    return out;
  }
}
'@

# services/analysis.service.ts
Write-Text "src/app/services/analysis.service.ts" @'
import { Injectable } from '@angular/core';
import { MealService } from './meal.service';

export interface Goals { calories: number; proteins: number; fats: number; carbs: number; }

const DEFAULT_GOALS: Goals = { calories: 2000, proteins: 100, fats: 70, carbs: 250 };

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  goals: Goals = { ...DEFAULT_GOALS };

  constructor(private meals: MealService) {}

  todayRecommendation() {
    const today = this.meals.sumForDate(new Date());
    const g = this.goals;
    return {
      consumed: today.totals,
      remaining: {
        calories: Math.max(0, g.calories - today.totals.calories),
        proteins: Math.max(0, g.proteins - today.totals.proteins),
        fats: Math.max(0, g.fats - today.totals.fats),
        carbs: Math.max(0, g.carbs - today.totals.carbs)
      },
      tip: this.tipForRemainder(g, today.totals)
    };
  }

  periodSummary(days: number) {
    const { totals, count } = this.meals.sumDaysBack(days);
    const avgDaily = {
      calories: totals.calories / days,
      proteins: totals.proteins / days,
      fats: totals.fats / days,
      carbs: totals.carbs / days
    };
    return {
      totals, days, entries: count, avgDaily,
      deviationFromGoals: {
        calories: avgDaily.calories - this.goals.calories,
        proteins: avgDaily.proteins - this.goals.proteins,
        fats: avgDaily.fats - this.goals.fats,
        carbs: avgDaily.carbs - this.goals.carbs
      },
      recommendation: this.recommendForPeriod(avgDaily)
    };
  }

  private tipForRemainder(goals: Goals, tot: any) {
    const remainCal = goals.calories - tot.calories;
    if (remainCal > 400) return 'До конца дня: сделай плотный приём пищи 400–700 ккал (белок 25–40 г).';
    if (remainCal > 150) return 'Подойдёт перекус 150–300 ккал, с акцентом на белок.';
    if (remainCal > 0) return 'Финишируй лёгким перекусом до 150 ккал.';
    return 'Лимит на сегодня достигнут — держи воду/чай, без лишних калорий.';
  }

  private recommendForPeriod(avg: any) {
    const over = avg.calories - this.goals.calories;
    if (over > 150) return 'В среднем переедание. Уменьши 200–300 ккал/день; добавь овощи и белок.';
    if (over < -150) return 'В среднем недобор. Добавь 150–250 ккал/день, держи белок ≥ 100 г.';
    return 'Баланс около цели. Продолжай в том же духе; следи за белком и клетчаткой.';
  }
}
'@

# pages/history
Write-Text "src/app/pages/history/history.page.ts" @'
import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MealService } from '../../services/meal.service';
import { Meal } from '../../models/meal';
import { MealDialogComponent } from '../../components/meal-dialog/meal-dialog.component';
import { Capacitor } from '@capacitor/core';

@Component({
  selector: 'app-history',
  templateUrl: './history.page.html',
  styleUrls: ['./history.page.scss']
})
export class HistoryPage implements OnInit {
  meals: Meal[] = [];
  constructor(private mealsSvc: MealService, private dialog: MatDialog) {}

  ngOnInit() { this.refresh(); }

  refresh() { this.meals = this.mealsSvc.getAll(); }

  formatTime(ms: number) { return new Date(ms).toLocaleString(); }

  imgSrc(uri?: string) {
    if (!uri) return '';
    return Capacitor.convertFileSrc(uri);
  }

  edit(m: Meal) {
    const ref = this.dialog.open(MealDialogComponent, { width: '380px', data: { meal: { ...m } } });
    ref.afterClosed().subscribe(res => {
      if (res) this.mealsSvc.update(m.id, res);
      this.refresh();
    });
  }

  remove(m: Meal) { this.mealsSvc.remove(m.id); this.refresh(); }
}
'@

Write-Text "src/app/pages/history/history.page.html" @'
<mat-card *ngFor="let m of meals" class="meal-card">
  <div class="row">
    <img *ngIf="m.photoUri" [src]="imgSrc(m.photoUri)" alt="meal" />
    <div class="info">
      <div class="time">{{ formatTime(m.timestamp) }}</div>
      <div class="kcal">{{ m.calories | number:'1.0-0' }} ккал</div>
      <div class="macros">Б {{ m.proteins || 0 }} г · Ж {{ m.fats || 0 }} г · У {{ m.carbs || 0 }} г</div>
      <div class="note" *ngIf="m.note">{{ m.note }}</div>
      <div class="actions">
        <button mat-stroked-button color="primary" (click)="edit(m)">Уточнить</button>
        <button mat-icon-button color="warn" (click)="remove(m)" aria-label="Удалить"><mat-icon>delete</mat-icon></button>
      </div>
    </div>
  </div>
</mat-card>

<style>
.meal-card { margin-bottom: 12px; }
.row { display: flex; gap: 12px; align-items: flex-start; }
img { width: 96px; height: 96px; object-fit: cover; border-radius: 8px; }
.info { flex: 1; }
.time { font-size: 12px; opacity: 0.7; }
.kcal { font-weight: 600; margin: 4px 0; }
.macros { font-size: 13px; }
.actions { margin-top: 8px; display: flex; gap: 8px; align-items: center; }
</style>
'@

Write-Text "src/app/pages/history/history.page.scss" @'
:host { display: block; }
'@

# pages/add-meal
Write-Text "src/app/pages/add-meal/add-meal.page.ts" @'
import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
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

  form = this.fb.group({
    calories: [null, [Validators.required, Validators.min(0)]],
    proteins: [0, [Validators.min(0)]],
    fats: [0, [Validators.min(0)]],
    carbs: [0, [Validators.min(0)]],
    note: ['']
  });

  constructor(private fb: FormBuilder, private meals: MealService, private storage: StorageService) {}

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
    const v = this.form.value;
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

Write-Text "src/app/pages/add-meal/add-meal.page.html" @'
<mat-card>
  <div class="photo">
    <img *ngIf="photoUri" [src]="photoUri | imgsrc" alt="preview" />
    <button mat-raised-button color="primary" (click)="takePhoto()">
      <mat-icon>photo_camera</mat-icon>
      Сделать фото
    </button>
  </div>

  <form [formGroup]="form" class="form">
    <mat-form-field appearance="outline">
      <mat-label>Калории (ккал)</mat-label>
      <input matInput type="number" formControlName="calories" />
    </mat-form-field>

    <div class="grid">
      <mat-form-field appearance="outline">
        <mat-label>Белки (г)</mat-label>
        <input matInput type="number" formControlName="proteins" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Жиры (г)</mat-label>
        <input matInput type="number" formControlName="fats" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Углеводы (г)</mat-label>
        <input matInput type="number" formControlName="carbs" />
      </mat-form-field>
    </div>

    <mat-form-field appearance="outline" class="full">
      <mat-label>Заметка</mat-label>
      <input matInput formControlName="note" />
    </mat-form-field>

    <button mat-raised-button color="accent" (click)="save()" [disabled]="form.invalid">
      <mat-icon>save</mat-icon>
      Сохранить
    </button>
  </form>
</mat-card>

<style>
.photo { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; }
img { width: 120px; height: 120px; object-fit: cover; border-radius: 10px; }
.form { display: grid; grid-template-columns: 1fr; gap: 12px; }
.grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.full { grid-column: 1 / -1; }
</style>
'@

Write-Text "src/app/pages/add-meal/add-meal.page.scss" @'
:host { display: block; }
'@

# pipes/imgsrc.pipe.ts
Write-Text "src/app/pipes/imgsrc.pipe.ts" @'
import { Pipe, PipeTransform } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Pipe({ name: 'imgsrc' })
export class ImgSrcPipe implements PipeTransform {
  transform(uri?: string): string {
    return uri ? Capacitor.convertFileSrc(uri) : '';
  }
}
'@

# components/meal-dialog
Write-Text "src/app/components/meal-dialog/meal-dialog.component.ts" @'
import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, Validators } from '@angular/forms';
import { VoiceService } from '../../services/voice.service';

@Component({
  selector: 'app-meal-dialog',
  templateUrl: './meal-dialog.component.html',
  styleUrls: ['./meal-dialog.component.scss']
})
export class MealDialogComponent {
  loadingVoice = false;

  form = this.fb.group({
    calories: [0, [Validators.required, Validators.min(0)]],
    proteins: [0, [Validators.min(0)]],
    fats: [0, [Validators.min(0)]],
    carbs: [0, [Validators.min(0)]],
    note: ['']
  });

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { meal: any },
    private ref: MatDialogRef<MealDialogComponent>,
    private fb: FormBuilder,
    private voice: VoiceService
  ) {
    const m = data.meal;
    this.form.patchValue({
      calories: m.calories || 0,
      proteins: m.proteins || 0,
      fats: m.fats || 0,
      carbs: m.carbs || 0,
      note: m.note || ''
    });
  }

  async speakFill() {
    this.loadingVoice = true;
    try {
      const text = await this.voice.listenOnce('ru-RU');
      const parsed = this.voice.parseMacros(text);
      this.form.patchValue(parsed);
    } catch (e) {
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

Write-Text "src/app/components/meal-dialog/meal-dialog.component.html" @'
<h2 mat-dialog-title>Уточнение данных</h2>
<mat-dialog-content>
  <form [formGroup]="form" class="form">
    <mat-form-field appearance="outline">
      <mat-label>Калории (ккал)</mat-label>
      <input matInput type="number" formControlName="calories" />
    </mat-form-field>

    <div class="grid">
      <mat-form-field appearance="outline">
        <mat-label>Белки (г)</mat-label>
        <input matInput type="number" formControlName="proteins" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Жиры (г)</mat-label>
        <input matInput type="number" formControlName="fats" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Углеводы (г)</mat-label>
        <input matInput type="number" formControlName="carbs" />
      </mat-form-field>
    </div>

    <mat-form-field appearance="outline" class="full">
      <mat-label>Заметка</mat-label>
      <input matInput formControlName="note" />
    </mat-form-field>
  </form>
</mat-dialog-content>
<mat-dialog-actions align="end">
  <button mat-stroked-button (click)="speakFill()" [disabled]="loadingVoice">
    <mat-icon>mic</mat-icon>
    Голосом
  </button>
  <button mat-raised-button color="primary" (click)="save()">Сохранить</button>
</mat-dialog-actions>

<style>
.form { display: grid; grid-template-columns: 1fr; gap: 12px; }
.grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.full { grid-column: 1 / -1; }
</style>
'@

Write-Text "src/app/components/meal-dialog/meal-dialog.component.scss" @'
:host { display: block; }
'@

# pages/analysis
Write-Text "src/app/pages/analysis/analysis.page.ts" @'
import { Component } from '@angular/core';
import { AnalysisService } from '../../services/analysis.service';

@Component({
  selector: 'app-analysis',
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage {
  constructor(public a: AnalysisService) {}
}
'@

Write-Text "src/app/pages/analysis/analysis.page.html" @'
<mat-card>
  <h3>Итог на сегодня</h3>
  <ng-container *ngIf="a.todayRecommendation() as t">
    <p><b>Съедено:</b> {{ t.consumed.calories | number:'1.0-0' }} ккал · Б {{ t.consumed.proteins | number:'1.0-0' }} · Ж {{ t.consumed.fats | number:'1.0-0' }} · У {{ t.consumed.carbs | number:'1.0-0' }}</p>
    <p><b>Осталось:</b> {{ t.remaining.calories | number:'1.0-0' }} ккал · Б {{ t.remaining.proteins | number:'1.0-0' }} · Ж {{ t.remaining.fats | number:'1.0-0' }} · У {{ t.remaining.carbs | number:'1.0-0' }}</p>
    <mat-chip-listbox>
      <mat-chip>{{ t.tip }}</mat-chip>
    </mat-chip-listbox>
  </ng-container>
</mat-card>

<mat-card>
  <h3>Периоды</h3>
  <div class="periods">
    <div class="period" *ngFor="let d of [7,30,90]">
      <h4 *ngIf="d===7">Неделя</h4>
      <h4 *ngIf="d===30">Месяц (30 дней)</h4>
      <h4 *ngIf="d===90">Квартал (90 дней)</h4>
      <ng-container *ngIf="a.periodSummary(d) as s">
        <p><b>Записей:</b> {{ s.entries }}, дней: {{ s.days }}</p>
        <p><b>Итого:</b> {{ s.totals.calories | number:'1.0-0' }} ккал</p>
        <p><b>Среднесуточно:</b> {{ s.avgDaily.calories | number:'1.0-0' }} ккал
        · Б {{ s.avgDaily.proteins | number:'1.0-0' }} · Ж {{ s.avgDaily.fats | number:'1.0-0' }} · У {{ s.avgDaily.carbs | number:'1.0-0' }}</p>
        <p><i>{{ s.recommendation }}</i></p>
      </ng-container>
    </div>
  </div>
</mat-card>

<style>
.periods { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.period { border: 1px dashed rgba(0,0,0,.2); padding: 8px; border-radius: 8px; }
</style>
'@

Write-Text "src/app/pages/analysis/analysis.page.scss" @'
:host { display: block; }
'@

# app files (routing/module/app)
Write-Text "src/app/app-routing.module.ts" @'
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HistoryPage } from './pages/history/history.page';
import { AddMealPage } from './pages/add-meal/add-meal.page';
import { AnalysisPage } from './pages/analysis/analysis.page';

const routes: Routes = [
  { path: '', redirectTo: 'history', pathMatch: 'full' },
  { path: 'history', component: HistoryPage },
  { path: 'add', component: AddMealPage },
  { path: 'analysis', component: AnalysisPage },
  { path: '**', redirectTo: 'history' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { useHash: true })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
'@

Write-Text "src/app/app.component.ts" @'
import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent {}
'@

Write-Text "src/app/app.component.html" @'
<mat-toolbar color="primary" class="topbar">
  <span>Calorie Counter</span>
  <span class="spacer"></span>
  <a mat-icon-button aria-label="История" routerLink="/history"><mat-icon>history</mat-icon></a>
  <a mat-icon-button aria-label="Добавить" routerLink="/add"><mat-icon>add_a_photo</mat-icon></a>
  <a mat-icon-button aria-label="Анализ" routerLink="/analysis"><mat-icon>analytics</mat-icon></a>
</mat-toolbar>

<div class="container">
  <router-outlet></router-outlet>
</div>

<style>
.topbar { position: sticky; top: 0; z-index: 10; }
.container { padding: 12px; }
.spacer { flex: 1 1 auto; }
</style>
'@

# app.module.ts (ensure BrowserAnimationsModule, declarations, MaterialModule, ImgSrcPipe)
Write-Text "src/app/app.module.ts" @'
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { MaterialModule } from './shared/material.module';

import { HistoryPage } from './pages/history/history.page';
import { AddMealPage } from './pages/add-meal/add-meal.page';
import { AnalysisPage } from './pages/analysis/analysis.page';
import { MealDialogComponent } from './components/meal-dialog/meal-dialog.component';
import { ImgSrcPipe } from './pipes/imgsrc.pipe';

@NgModule({
  declarations: [
    AppComponent,
    HistoryPage,
    AddMealPage,
    AnalysisPage,
    MealDialogComponent,
    ImgSrcPipe
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    FormsModule,
    ReactiveFormsModule,
    MaterialModule,
    AppRoutingModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {}
'@

# index.html: ensure Material Icons
$indexPath = "src/index.html"
if (Test-Path $indexPath) {
  $index = Get-Content -LiteralPath $indexPath -Raw
  if ($index -notmatch 'fonts\.googleapis\.com/icon') {
    $index = $index -replace '(</head>)', "  <link href=`"https://fonts.googleapis.com/icon?family=Material+Icons`" rel=`"stylesheet`">`n`$1"
    Set-Content -LiteralPath $indexPath -Value $index -Encoding UTF8
    Write-Host "Added Material Icons link to index.html"
  }
}

# --- 5) Android permissions ---
$manifest = "android/app/src/main/AndroidManifest.xml"
Add-AndroidPermission $manifest '<uses-permission android:name="android.permission.CAMERA" />'
Add-AndroidPermission $manifest '<uses-permission android:name="android.permission.RECORD_AUDIO" />'
Add-AndroidPermission $manifest '<uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />'
Add-AndroidPermission $manifest '<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />'
Add-AndroidPermission $manifest '<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" android:maxSdkVersion="28" />'

# --- 6) build & cap sync ---
Write-Host "Installing dependencies (npm ci) ..."
npm ci

Write-Host "Building Angular app ..."
ng build

Write-Host "Capacitor sync ..."
npx cap sync

Write-Host "`nDone! Open Android Studio:"
Write-Host "  npx cap open android"
Write-Host "Or run on device/emulator:"
Write-Host "  npx cap run android"
