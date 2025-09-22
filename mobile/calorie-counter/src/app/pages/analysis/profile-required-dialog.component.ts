import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

type DialogResult = 'close' | 'profile';

@Component({
  selector: 'app-profile-required-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Заполните профиль</h2>
    <mat-dialog-content>
      <p class="message">
        Чтобы получить рекомендации пищевого коуча, заполните профиль в разделе «Профиль».
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end" class="actions">
      <button
        type="button"
        class="icon-button icon-button--neutral"
        (click)="close()"
        aria-label="Закрыть диалог"
      >
        <span aria-hidden="true">✕</span>
      </button>
      <button
        type="button"
        class="icon-button icon-button--primary"
        (click)="goToProfile()"
        aria-label="Перейти в профиль"
      >
        <span aria-hidden="true">👤</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .message {
        margin: 0;
        font-size: 15px;
        line-height: 1.5;
        color: #1f2937;
      }

      .actions {
        display: flex;
        align-items: center;
        gap: 12px;
        margin-top: 8px;
      }

      .icon-button {
        border: none;
        background: transparent;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 44px;
        height: 44px;
        border-radius: 50%;
        font-size: 24px;
        line-height: 1;
        cursor: pointer;
        color: #1f2937;
        box-shadow: 0 2px 6px rgba(15, 23, 42, 0.15);
        transition: transform 0.12s ease, box-shadow 0.12s ease, opacity 0.2s ease;
      }

      .icon-button[disabled] {
        opacity: 0.4;
        cursor: default;
        box-shadow: none;
      }

      .icon-button:not([disabled]):active {
        transform: translateY(1px) scale(0.97);
        box-shadow: 0 1px 3px rgba(15, 23, 42, 0.2);
      }

      .icon-button:focus-visible {
        outline: 2px solid rgba(59, 130, 246, 0.6);
        outline-offset: 2px;
      }

      .icon-button--primary {
        color: #2563eb;
      }

      .icon-button--neutral {
        color: #4b5563;
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
