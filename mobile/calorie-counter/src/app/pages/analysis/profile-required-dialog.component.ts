import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

type DialogResult = 'close' | 'profile';

@Component({
  selector: 'app-profile-required-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Профиль нужен для рекомендаций</h2>
    <mat-dialog-content>
      <p class="message">
        Чтобы создать персональный отчёт, заполните обязательные поля в разделе «Профиль»
        (пол, год рождения, рост, вес и активность).
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="actions">
      <button type="button" mat-button (click)="close()">Позже</button>
      <button type="button" mat-raised-button color="primary" (click)="goToProfile()">
        Заполнить профиль
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .message {
        margin: 0;
        font-size: 15px;
        line-height: 1.4;
        color: #1f2937;
      }

      .actions {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-top: 8px;
      }
    `
  ]
})
export class ProfileRequiredDialogComponent {
  constructor(private readonly dialogRef: MatDialogRef<ProfileRequiredDialogComponent, DialogResult>) {}

  close() {
    this.dialogRef.close('close');
  }

  goToProfile() {
    this.dialogRef.close('profile');
  }
}
