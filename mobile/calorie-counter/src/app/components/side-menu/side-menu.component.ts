import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RouterModule } from '@angular/router';

import { AddMealNoteDialogComponent } from '../../pages/add-meal/add-meal-note-dialog.component';

@Component({
  selector: 'app-side-menu',
  standalone: true,
  imports: [CommonModule, MatListModule, MatIconModule, MatDialogModule, RouterModule],
  templateUrl: './side-menu.component.html',
  styleUrls: ['./side-menu.component.scss'],
})
export class SideMenuComponent {
  @Output() close = new EventEmitter<void>();

  constructor(private dialog: MatDialog) {}

  openAddMealNoteDialog(event?: Event) {
    event?.preventDefault();
    const alreadyOpen = this.dialog.openDialogs.some(
      d => d.componentInstance instanceof AddMealNoteDialogComponent
    );
    if (alreadyOpen) {
      this.close.emit();
      return;
    }

    this.close.emit();
    this.dialog.open(AddMealNoteDialogComponent, {
      width: 'min(480px, 90vw)',
      maxWidth: '90vw',
      autoFocus: false,
      restoreFocus: false
    });
  }
}
