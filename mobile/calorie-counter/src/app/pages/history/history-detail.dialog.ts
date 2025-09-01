import { Component, Inject, OnInit, ViewChild, ElementRef } from "@angular/core";
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

  @ViewChild('photo') photo?: ElementRef<HTMLImageElement>;

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
        this.fitDialog(); // поджать после прихода данных
      },
      error: () => {}
    });

    // небольшая «пинок»-подстройка сразу после первой отрисовки
    this.fitDialog();
  }

  /** Аккуратно подгоняет высоту диалога под контент */
  fitDialog() {
    // UpdateSize ставит стиль на .cdk-overlay-pane;
    // ширину подгоняем под фото с учетом размеров экрана
    Promise.resolve().then(() => {
      const img = this.photo?.nativeElement;
      if (!img) {
        this.dialogRef.updateSize('', 'auto');
        return;
      }
      const maxW = window.innerWidth * 0.9;
      const maxH = window.innerHeight * 0.55;
      let w = img.naturalWidth;
      const h = img.naturalHeight;
      if (h > maxH) {
        w = w * (maxH / h);
      }
      w = Math.min(w, maxW);
      this.dialogRef.updateSize(`${Math.round(w)}px`, 'auto');
    });
  }

  openClarify() {
    const ref = this.dialog.open(HistoryClarifyDialogComponent, { data: { mealId: this.data.item.id } });
    ref.afterClosed().subscribe((r: ClarifyResult | { deleted: true } | undefined) => {
      if (!r) return;
      if ((r as any).deleted) {
        this.snack.open('Запись удалена', 'OK', { duration: 1500 });
        this.dialogRef.close({ deleted: true });
        return;
      }
      const res = r as ClarifyResult;
      this.data.item.dishName = res.result.dish;
      this.data.item.caloriesKcal = res.result.calories_kcal;
      this.data.item.proteinsG = res.result.proteins_g;
      this.data.item.fatsG = res.result.fats_g;
      this.data.item.carbsG = res.result.carbs_g;
      this.data.item.weightG = res.result.weight_g;
      this.data.item.ingredients = res.result.ingredients;
      this.data.item.products = res.products;
      this.snack.open('Уточнение применено', 'OK', { duration: 1500 });
      this.dialogRef.close();
    });
  }

  remove() {
    if (!confirm('Удалить запись?')) return;
    this.api.deleteMeal(this.data.item.id).subscribe({
      next: () => {
        this.snack.open('Запись удалена', 'OK', { duration: 1500 });
        this.dialogRef.close({ deleted: true });
      },
      error: () => {
        this.snack.open('Не удалось удалить', 'OK', { duration: 1500 });
      }
    });
  }
}
