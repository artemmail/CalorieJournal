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
