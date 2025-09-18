import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RouterModule } from '@angular/router';

import { VoiceNoteDialogComponent } from '../voice-note-dialog/voice-note-dialog.component';

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
      d => d.componentInstance instanceof VoiceNoteDialogComponent
    );
    if (alreadyOpen) {
      this.close.emit();
      return;
    }

    this.close.emit();
    this.dialog.open(VoiceNoteDialogComponent, {
      width: 'min(480px, 90vw)',
      maxWidth: '90vw',
      autoFocus: false,
      restoreFocus: false,
      data: {
        title: 'Описание приёма пищи',
        kind: 'addMeal'
      }
    });
  }
}
