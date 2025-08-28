import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FoodbotApiService } from '../../services/foodbot-api.service';
import { PersonalCard } from '../../services/foodbot-api.types';

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
  form = this.fb.group({
    email: ['', [Validators.email]],
    name: [''],
    birthYear: [null as number | null],
    dietGoals: [''],
    medicalRestrictions: ['']
  });

  constructor(private fb: FormBuilder, private api: FoodbotApiService, private snack: MatSnackBar) {}

  ngOnInit() {
    const currentYear = new Date().getFullYear();
    for (let y = currentYear; y >= 1900; y--) this.years.push(y);
    this.api.getPersonalCard().subscribe(card => {
      if (card) this.form.patchValue(card);
    });
  }

  save() {
    if (this.form.invalid) return;
    const card = this.form.value as PersonalCard;
    this.api.savePersonalCard(card).subscribe({
      next: () => this.snack.open('Сохранено', 'OK', { duration: 1500 }),
      error: () => this.snack.open('Ошибка сохранения', 'OK', { duration: 1500 })
    });
  }
}
