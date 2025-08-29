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
  template: `
    <div class="container">
      <img *ngIf="data.item.hasImage && data.imageUrl" [src]="data.imageUrl" alt="meal" class="photo" />

      <div class="macros">
        Б {{ data.item.proteinsG ?? '—' }} · Ж {{ data.item.fatsG ?? '—' }} · У {{ data.item.carbsG ?? '—' }} · {{ data.item.caloriesKcal ?? '—' }} ккал
      </div>

      <div class="ingredients" *ngIf="data.item.ingredients?.length">
        Ингредиенты: {{ data.item.ingredients.join(', ') }}
      </div>

      <!-- Компактная таблица состава -->
      <div *ngIf="(data.item.products?.length || 0) > 0" class="composition">
        <table mat-table [dataSource]="data.item.products" class="products-table mat-elevation-z1">

          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef class="h name">Продукт</th>
            <td mat-cell *matCellDef="let p" class="c name">{{ p.name }}</td>
          </ng-container>

          <ng-container matColumnDef="percent">
            <th mat-header-cell *matHeaderCellDef class="h num">% </th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.percent == null ? '—' : (p.percent | number:'1.0-1') }}
            </td>
          </ng-container>

          <ng-container matColumnDef="grams">
            <th mat-header-cell *matHeaderCellDef class="h num">г</th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.grams == null ? '—' : (p.grams | number:'1.0-0') }}
            </td>
          </ng-container>

          <ng-container matColumnDef="proteins_g">
            <th mat-header-cell *matHeaderCellDef class="h num">Б</th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.proteins_g == null ? '—' : (p.proteins_g | number:'1.0-1') }}
            </td>
          </ng-container>

          <ng-container matColumnDef="fats_g">
            <th mat-header-cell *matHeaderCellDef class="h num">Ж</th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.fats_g == null ? '—' : (p.fats_g | number:'1.0-1') }}
            </td>
          </ng-container>

          <ng-container matColumnDef="carbs_g">
            <th mat-header-cell *matHeaderCellDef class="h num">У</th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.carbs_g == null ? '—' : (p.carbs_g | number:'1.0-1') }}
            </td>
          </ng-container>

          <ng-container matColumnDef="calories_kcal">
            <th mat-header-cell *matHeaderCellDef class="h num">ккал</th>
            <td mat-cell *matCellDef="let p" class="c num">
              {{ p.calories_kcal == null ? '—' : (p.calories_kcal | number:'1.0-0') }}
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns" class="row head"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;" class="row"></tr>
        </table>
      </div>

      <div class="buttons">
        <button mat-raised-button color="primary" (click)="openClarify()">Уточнить</button>
        <button mat-button (click)="dialogRef.close()">Закрыть</button>
      </div>
    </div>
  `,
  styles: [`
    /* Общая плотность */

:host ::ng-deep .mat-mdc-table .mdc-data-table__row,
:host ::ng-deep .mat-mdc-table .mdc-data-table__header-row {
  height: auto;
}

/* Вертикальный паддинг именно для mdc-ячеёк */
:host ::ng-deep .mat-mdc-table .mdc-data-table__cell,
:host ::ng-deep .mat-mdc-table .mdc-data-table__header-cell {
  padding-top: 2px;   /* ← ТУТ по высоте */
  padding-bottom: 2px;
  padding-left: 6px;  /* ширина — ок */
  padding-right: 6px;
}

    .container { display: flex; flex-direction: column; gap: 1x; height: 100%; overflow: auto; padding: 12px; }
    .photo { width: 100%; height: auto; max-height: 55vh; border-radius: 6px; object-fit: contain; }
    .macros { font-size: 13px; line-height: 1.2; }
    .ingredients { font-size: 12.5px; line-height: 1.2; }

    /* Таблица компактная, не растягивается на всю ширину */
    .composition { align-self: flex-start; max-width: 100%; overflow-x: auto; }
    .products-table {
      display: inline-table;          /* позволяет занять ширину по контенту */
      width: auto;
      border-collapse: collapse;      /* убираем лишние промежутки */
      font-size: 12.5px;
      line-height: 1.15;
      white-space: nowrap;            /* компактные ячейки */
      font-variant-numeric: tabular-nums; /* ровные цифры */
    }

    /* Паддинги минимальные */
    .products-table .h, .products-table .c {
      padding: 0px 6px;
    }

    /* Заголовок чуть темнее и фиксированной высоты */
    .products-table .h {
      font-weight: 600;
      border-bottom: 1px solid rgba(0,0,0,.12);
    }

    /* Тонкие разделители строк */
    .products-table .c {
      border-bottom: 1px dashed rgba(0,0,0,.08);
    }

    /* Наведение без сильной «воздушности» */
    .products-table .row:hover .c {
      background: rgba(0,0,0,.03);
    }

    /* Выровнять числа вправо для компактности */
    .products-table .num { text-align: right; }

    /* Колонки: имя оставляем эластичной, остальным задаём минимальную ширину */
    .products-table .name { max-width: 380px; overflow: hidden; text-overflow: ellipsis; }
    @media (max-width: 420px) {
      .products-table .name { max-width: 240px; }
    }

    .buttons { display: flex; justify-content: flex-end; gap: 6px; }
  `]
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
