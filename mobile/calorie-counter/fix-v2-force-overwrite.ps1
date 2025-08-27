param()

function Write-Text($Path, $Content) {
  $dir = Split-Path -Parent $Path
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8 -Force
  Write-Host "Wrote: $Path"
}

# 0) Удалим NgModule-артефакты (если есть)
$toDelete = @(
  "src/app/app.module.ts",
  "src/app/app-routing.module.ts",
  "src/app/shared/material.module.ts"
)
$toDelete | ForEach-Object {
  if (Test-Path $_) { Remove-Item $_ -Force; Write-Host "Deleted: $_" }
}

# 1) main.ts -> bootstrapApplication + providers
Write-Text "src/main.ts" @'
import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { routes } from './app/routes';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideAnimations()
  ]
}).catch(err => console.error(err));
'@

# 2) routes.ts (standalone-компоненты можно указывать прямо в route.component)
Write-Text "src/app/routes.ts" @'
import { Routes } from '@angular/router';
import { HistoryPage } from './pages/history/history.page';
import { AddMealPage } from './pages/add-meal/add-meal.page';
import { AnalysisPage } from './pages/analysis/analysis.page';

export const routes: Routes = [
  { path: '', redirectTo: 'history', pathMatch: 'full' },
  { path: 'history', component: HistoryPage },
  { path: 'add', component: AddMealPage },
  { path: 'analysis', component: AnalysisPage },
  { path: '**', redirectTo: 'history' }
];
'@

# 3) app.component.ts -> standalone + нужные импорты
Write-Text "src/app/app.component.ts" @'
import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { NgIf } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, NgIf, MatToolbarModule, MatIconModule, MatButtonModule],
  templateUrl: './app.component.html'
})
export class AppComponent {}
'@

# 4) HistoryPage -> standalone
Write-Text "src/app/pages/history/history.page.ts" @'
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MealService } from '../../services/meal.service';
import { Meal } from '../../models/meal';
import { MealDialogComponent } from '../../components/meal-dialog/meal-dialog.component';
import { Capacitor } from '@capacitor/core';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatDialogModule],
  templateUrl: './history.page.html',
  styleUrls: ['./history.page.scss']
})
export class HistoryPage implements OnInit {
  meals: Meal[] = [];
  constructor(private mealsSvc: MealService, private dialog: MatDialog) {}
  ngOnInit() { this.refresh(); }
  refresh() { this.meals = this.mealsSvc.getAll(); }
  formatTime(ms: number) { return new Date(ms).toLocaleString(); }
  imgSrc(uri?: string) { return uri ? Capacitor.convertFileSrc(uri) : ''; }
  edit(m: Meal) {
    const ref = this.dialog.open(MealDialogComponent, { width: '380px', data: { meal: { ...m } } });
    ref.afterClosed().subscribe(res => { if (res) this.mealsSvc.update(m.id, res); this.refresh(); });
  }
  remove(m: Meal) { this.mealsSvc.remove(m.id); this.refresh(); }
}
'@

# 5) AddMealPage -> standalone
Write-Text "src/app/pages/add-meal/add-meal.page.ts" @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { MealService } from '../../services/meal.service';
import { StorageService } from '../../services/storage.service';
import { ImgSrcPipe } from '../../pipes/imgsrc.pipe';

@Component({
  selector: 'app-add-meal',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
    ImgSrcPipe
  ],
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
    const img = await Camera.getPhoto({ quality: 70, resultType: CameraResultType.Base64, source: CameraSource.Camera });
    if (img.base64String) this.photoUri = await this.storage.saveImageBase64ToDataDir(img.base64String);
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
    this.form.reset(); this.photoUri = undefined; alert('Сохранено');
  }
}
'@

# 6) AnalysisPage -> standalone
Write-Text "src/app/pages/analysis/analysis.page.ts" @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { AnalysisService } from '../../services/analysis.service';

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatChipsModule],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage {
  constructor(public a: AnalysisService) {}
}
'@

# 7) MealDialogComponent -> standalone
Write-Text "src/app/components/meal-dialog/meal-dialog.component.ts" @'
import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { VoiceService } from '../../services/voice.service';

@Component({
  selector: 'app-meal-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule
  ],
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
    } catch { alert('Не удалось распознать речь'); }
    finally { this.loadingVoice = false; }
  }

  save() { if (this.form.invalid) return; this.ref.close(this.form.value); }
}
'@

# 8) ImgSrcPipe -> standalone
Write-Text "src/app/pipes/imgsrc.pipe.ts" @'
import { Pipe, PipeTransform } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Pipe({ name: 'imgsrc', standalone: true })
export class ImgSrcPipe implements PipeTransform {
  transform(uri?: string): string { return uri ? Capacitor.convertFileSrc(uri) : ''; }
}
'@

# 9) На всякий случай — убрать следы standalone:false/true в старых файлах (мягкая чистка)
Get-ChildItem -Path "src/app" -Recurse -Include *.ts | ForEach-Object {
  $p = $_.FullName
  $c = Get-Content -LiteralPath $p -Raw
  # убираем пустые 'imports:' после автогенераторов
  $c2 = [regex]::Replace($c, '(?s)imports\s*:\s*\[\s*\]\s*,?', '')
  if ($c2 -ne $c) { Set-Content -LiteralPath $p -Value $c2 -Encoding UTF8 }
}

Write-Host "`nStandalone conversion complete. Now run: ng build"
