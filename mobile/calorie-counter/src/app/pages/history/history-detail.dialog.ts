import { Component, Inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem, ClarifyResult } from "../../services/foodbot-api.types";

@Component({
  selector: 'app-history-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule
  ],
  template: `
    <div class="container">
      <img *ngIf="data.item.hasImage && data.imageUrl" [src]="data.imageUrl" alt="meal" class="photo" />
      <div class="macros">
        Б {{ data.item.proteinsG ?? '—' }} · Ж {{ data.item.fatsG ?? '—' }} · У {{ data.item.carbsG ?? '—' }} · {{ data.item.caloriesKcal ?? '—' }} ккал
      </div>
      <div class="ingredients" *ngIf="data.item.ingredients?.length">Ингредиенты: {{ data.item.ingredients.join(', ') }}</div>
      <div class="products" *ngIf="data.item.products?.length">
        <div *ngFor="let p of data.item.products">
          {{ p.name }} — {{ p.percent }}% ({{ p.grams }} г): Б {{ p.proteins_g }} · Ж {{ p.fats_g }} · У {{ p.carbs_g }} · {{ p.calories_kcal }} ккал
        </div>
      </div>
      <mat-form-field appearance="fill" class="note-field">
        <mat-label>Уточнение</mat-label>
        <textarea matInput [(ngModel)]="note"></textarea>
      </mat-form-field>
      <div class="buttons">
        <button mat-raised-button color="primary" (click)="onClarify()">Отправить</button>
        <button mat-button (click)="dialogRef.close()">Закрыть</button>
      </div>
    </div>
  `,
  styles: [`
    .container { display: flex; flex-direction: column; gap: 12px; height: 100%; overflow: auto; }
    .photo { width: 100%; height: auto; border-radius: 8px; object-fit: cover; }
    .macros { font-size: 14px; }
    .ingredients, .products { font-size: 13px; }
    .note-field { width: 100%; }
    .buttons { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class HistoryDetailDialogComponent {
  note = '';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { item: MealListItem; imageUrl: string },
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    public dialogRef: MatDialogRef<HistoryDetailDialogComponent>
  ) {}

  onClarify() {
    const note = this.note.trim();
    if (!note) return;
    this.api.clarifyText(this.data.item.id, note).subscribe({
      next: (r: ClarifyResult) => {
        this.data.item.dishName = r.result.dish;
        this.data.item.caloriesKcal = r.result.calories_kcal;
        this.data.item.proteinsG = r.result.proteins_g;
        this.data.item.fatsG = r.result.fats_g;
        this.data.item.carbsG = r.result.carbs_g;
        this.data.item.weightG = r.result.weight_g;
        this.data.item.ingredients = r.result.ingredients;
        this.data.item.products = r.products;
        this.snack.open('Уточнение применено', 'OK', { duration: 1500 });
        this.dialogRef.close();
      },
      error: () => this.snack.open('Ошибка уточнения', 'OK', { duration: 1500 })
    });
  }
}

