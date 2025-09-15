import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { PersonalCard, Gender, ActivityLevel } from '../../services/foodbot-api.types';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatSnackBarModule
  ],
  templateUrl: './profile.page.html',
  styleUrls: ['./profile.page.scss']
})
export class ProfilePage implements OnInit {
  years: number[] = [];
  form!: FormGroup;
  genders = [
    { value: 'male' as Gender, label: 'Мужской' },
    { value: 'female' as Gender, label: 'Женский' }
  ];
  activities = [
    { value: 'minimal' as ActivityLevel, label: 'Минимальная активность' },
    { value: 'light' as ActivityLevel, label: 'Лёгкая активность' },
    { value: 'moderate' as ActivityLevel, label: 'Средняя активность' },
    { value: 'high' as ActivityLevel, label: 'Высокая активность' },
    { value: 'veryHigh' as ActivityLevel, label: 'Очень высокая активность' },
  ];

  constructor(private fb: FormBuilder, private api: FoodbotApiService, private snack: MatSnackBar) {
    this.form = this.fb.group({
      email: ['', [Validators.email]],
      name: [''],
      birthYear: [null as number | null],
      gender: [null as Gender | null],
      heightCm: [null as number | null],
      weightKg: [null as number | null],
      activityLevel: [null as ActivityLevel | null],
      dailyCalories: [{ value: null as number | null, disabled: true }],
      dietGoals: [''],
      medicalRestrictions: ['']
    });
  }

  ngOnInit() {
    const currentYear = new Date().getFullYear();
    for (let y = currentYear; y >= 1900; y--) this.years.push(y);
    this.api.getPersonalCard().subscribe(card => {
      if (card) {
        this.form.patchValue(card);
        this.form.get('dailyCalories')!.setValue(card.dailyCalories, { emitEvent: false });
        this.recalcCalories();
      }
    });
    this.form.valueChanges.subscribe(() => this.recalcCalories());
  }

  save() {
    if (this.form.invalid) return;
    const card = this.form.getRawValue() as PersonalCard;
    this.api.savePersonalCard(card).subscribe({
      next: () => this.snack.open('Сохранено', 'OK', { duration: 1500 }),
      error: () => this.snack.open('Ошибка сохранения', 'OK', { duration: 1500 })
    });
  }

  private recalcCalories() {
    const { gender, heightCm, weightKg, birthYear, activityLevel } =
      this.form.getRawValue() as PersonalCard;
    if (gender && heightCm && weightKg && birthYear && activityLevel) {
      const age = new Date().getFullYear() - birthYear;
      const bmr = gender === 'male'
        ? 10 * weightKg + 6.25 * heightCm - 5 * age + 5
        : 10 * weightKg + 6.25 * heightCm - 5 * age - 161;
      const palMap: Record<ActivityLevel, number> = {
        minimal: 1.2,
        light: 1.375,
        moderate: 1.55,
        high: 1.725,
        veryHigh: 1.9,
      };
      const daily = Math.round(bmr * palMap[activityLevel]);
      this.form.get('dailyCalories')!.setValue(daily, { emitEvent: false });
    } else {
      this.form.get('dailyCalories')!.setValue(null, { emitEvent: false });
    }
  }
}
