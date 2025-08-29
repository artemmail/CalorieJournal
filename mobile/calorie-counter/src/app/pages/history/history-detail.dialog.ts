import { Component, Inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTableModule } from "@angular/material/table";
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
    MatSnackBarModule,
    MatTableModule
  ],
  templateUrl: './history-detail.dialog.html',
  styleUrls: ['./history-detail.dialog.scss']
})
export class HistoryDetailDialogComponent implements OnInit {
  displayedColumns = ['name','percent','grams','proteins_g','fats_g','carbs_g','calories_kcal'];

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
