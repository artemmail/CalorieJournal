import { Component, Inject, OnInit, AfterViewInit, ViewChild, ElementRef, HostListener } from "@angular/core";
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
export class HistoryDetailDialogComponent implements OnInit, AfterViewInit {
  displayedColumns = ['name','percent','grams','proteins_g','fats_g','carbs_g','calories_kcal'];

  @ViewChild('photo') photo?: ElementRef<HTMLImageElement>;

  private clarifyNote = '';

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
        this.clarifyNote = d.clarifyNote ?? '';
        this.fitDialog(); // поджать после прихода данных
      },
      error: () => {}
    });

    // первичная подстройка (если без фото)
    this.fitDialog();
  }

  ngAfterViewInit() {
    // если фото уже в кэше — сразу подгоняем, иначе ждём load
    const img = this.photo?.nativeElement;
    if (!img) return;
    if (img.complete) {
      this.fitDialog();
    } else {
      img.addEventListener('load', () => this.fitDialog(), { once: true });
      img.addEventListener('error', () => this.fitDialog(), { once: true });
    }
  }

  @HostListener('window:resize')
  onResize() {
    this.fitDialog();
  }

  /** Аккуратно подгоняет ширину диалога под фото/экран, высота — auto */
  fitDialog() {
    Promise.resolve().then(() => {
      const vw = Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0);
      const vh = Math.max(document.documentElement.clientHeight || 0, window.innerHeight || 0);

      const maxW = Math.round(vw * 0.9);   // как и в истории — «контейнер»
      const maxH = Math.round(vh * 0.78);  // диалог не выше 78vh, остальное прокручивается
      const minW = 360;                    // чтобы таблица/контент не схлопывались

      const img = this.photo?.nativeElement;

      let targetW: number;

      if (img && img.naturalWidth > 0 && img.naturalHeight > 0) {
        // подгоняем ширину так, чтобы фото вписалось по высоте,
        // но не выходило за 90vw и не было уже minW
        const ratio = img.naturalWidth / img.naturalHeight;
        const byHeight = Math.floor(Math.min(maxH, img.naturalHeight)); // ограничиваем по высоте
        targetW = Math.floor(byHeight * ratio);
        targetW = Math.min(targetW, maxW);
        targetW = Math.max(targetW, minW);
      } else {
        // нет фото — просто разумная ширина
        targetW = Math.min(Math.max(520, minW), maxW);
      }

      this.dialogRef.updateSize(`${targetW}px`, 'auto');
    });
  }

  openClarify() {
    const ref = this.dialog.open(HistoryClarifyDialogComponent, {
      data: { mealId: this.data.item.id, createdAtUtc: this.data.item.createdAtUtc, note: this.clarifyNote }
    });
    ref.afterClosed().subscribe((r: (ClarifyResult & { note?: string }) | { deleted: true } | { queued: true; note?: string } | undefined) => {
      if (!r) return;
      if ('deleted' in r && r.deleted) {
        this.snack.open('Запись удалена', 'OK', { duration: 1500 });
        this.dialogRef.close({ deleted: true });
        return;
      }
      if ('queued' in r && r.queued) {
        this.clarifyNote = r.note ?? this.clarifyNote;
        this.data.item.updateQueued = true;
        this.snack.open('Уточнение отправлено', 'OK', { duration: 1500 });
        return;
      }
      if ('note' in r) {
        this.clarifyNote = r.note ?? this.clarifyNote;
      }
      const res = r as ClarifyResult;
      this.data.item.createdAtUtc = res.createdAtUtc;
      this.data.item.dishName = res.result.dish;
      this.data.item.caloriesKcal = res.result.calories_kcal;
      this.data.item.proteinsG = res.result.proteins_g;
      this.data.item.fatsG = res.result.fats_g;
      this.data.item.carbsG = res.result.carbs_g;
      this.data.item.weightG = res.result.weight_g;
      this.data.item.ingredients = res.result.ingredients;
      this.data.item.products = res.products;
      this.data.item.updateQueued = false;
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
