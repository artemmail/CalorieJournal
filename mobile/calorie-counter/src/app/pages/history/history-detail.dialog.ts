import { Component, Inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem, ClarifyResult } from "../../services/foodbot-api.types";
import { HistoryClarifyDialogComponent } from "./history-clarify.dialog";

@Component({
  selector: 'app-history-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  template: `
    <div class="container">
      <img *ngIf="data.item.hasImage && data.imageUrl" [src]="data.imageUrl" alt="meal" class="photo" />
      <div class="macros">
        Б {{ data.item.proteinsG ?? '—' }} · Ж {{ data.item.fatsG ?? '—' }} · У {{ data.item.carbsG ?? '—' }} · {{ data.item.caloriesKcal ?? '—' }} ккал
      </div>
      <div class="ingredients" *ngIf="data.item.ingredients?.length">Ингредиенты: {{ data.item.ingredients.join(', ') }}</div>
      <div class="composition" *ngIf="data.item.products?.length">
        <div *ngFor="let p of data.item.products">
          {{ p.name }} — {{ p.percent }}% ({{ p.grams }} г): Б {{ p.proteins_g }} · Ж {{ p.fats_g }} · У {{ p.carbs_g }} · {{ p.calories_kcal }} ккал
        </div>
      </div>
      <div class="buttons">
        <button mat-raised-button color="primary" (click)="openClarify()">Уточнить</button>
        <button mat-button (click)="dialogRef.close()">Закрыть</button>
      </div>
    </div>
  `,
  styles: [`
    .container { display: flex; flex-direction: column; gap: 12px; height: 100%; overflow: auto; padding: 16px; }
    .photo { width: 100%; height: auto; max-height: 60vh; border-radius: 8px; object-fit: contain; }
    .macros { font-size: 14px; }
    .ingredients, .composition { font-size: 13px; }
    .buttons { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class HistoryDetailDialogComponent implements OnInit {

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { item: MealListItem; imageUrl: string },
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private dialog: MatDialog,
    public dialogRef: MatDialogRef<HistoryDetailDialogComponent>
  ) {}

  ngOnInit() {
    this.api.getMeal(this.data.item.id).subscribe({
      next: d => {
        this.data.item.ingredients = d.ingredients;
        this.data.item.products = d.products;
      },
      error: () => {}
    });
  }

  openClarify() {
    const ref = this.dialog.open(HistoryClarifyDialogComponent, { data: { mealId: this.data.item.id } });
    ref.afterClosed().subscribe((r: ClarifyResult | undefined) => {
      if (!r) return;
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
    });
  }
}

