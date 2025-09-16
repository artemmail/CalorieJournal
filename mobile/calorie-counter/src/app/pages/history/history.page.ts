import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { InfiniteScrollModule } from "ngx-infinite-scroll";
import { Router } from "@angular/router";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem } from "../../services/foodbot-api.types";
import { FoodBotAuthLinkService } from "../../services/foodbot-auth-link.service";
import { HistoryDetailDialogComponent } from "./history-detail.dialog";
import { HistoryUpdatesService } from "../../services/history-updates.service";
import { Subscription } from "rxjs";

@Component({
  selector: "app-history",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    InfiniteScrollModule,
    MatSnackBarModule,
    MatDialogModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./history.page.html",
  styleUrls: ["./history.page.scss"]
})
export class HistoryPage implements OnInit, OnDestroy {
  items: MealListItem[] = [];
  total = 0;
  pageSize = 10;
  loading = false;

  imageUrls = new Map<number, string>();
  private dateTotals = new Map<string, number>();
  private updatesSub?: Subscription;

  /** локальное состояние раскрытых ингредиентов */
  private ingOpen = new Set<number>();

  constructor(
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private router: Router,
    private auth: FoodBotAuthLinkService,
    private dialog: MatDialog,
    private updates: HistoryUpdatesService
  ) {}

  ngOnInit() {
    if (!this.auth.isAuthenticated()) {
      this.router.navigateByUrl("/auth");
      return;
    }
    this.loadMore();

    this.updatesSub = this.updates.updates().subscribe(item => {
      const idx = this.items.findIndex(x => x.id === item.id);
      if (idx >= 0) {
        this.items[idx] = item;
      } else {
        this.items = [item, ...this.items];
        this.total++;
      }
      this.items.sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());
      if (item.hasImage) {
        this.api.getMealImageObjectUrl(item.id).subscribe(url => this.imageUrls.set(item.id, url));
      }
      this.recomputeDateTotals();
    });
  }

  ngOnDestroy() {
    for (const url of this.imageUrls.values()) URL.revokeObjectURL(url);
    this.updatesSub?.unsubscribe();
  }

  loadMore() {
    if (this.loading) return;
    if (this.items.length >= this.total && this.total !== 0) return;

    this.loading = true;
    const offset = this.items.length;

    this.api.getMeals(this.pageSize, offset).subscribe({
      next: (res) => {
        this.items = [...this.items, ...res.items];
        this.total = res.total;
        for (const m of res.items) {
          if (m.hasImage) {
            this.api.getMealImageObjectUrl(m.id).subscribe((url) => this.imageUrls.set(m.id, url));
          }
        }
        this.recomputeDateTotals();
        this.loading = false;
      },
      error: () => {
        this.snack.open("Не удалось загрузить историю", "OK", { duration: 2000 });
        this.loading = false;
      }
    });
  }

  onScrollDown() { this.loadMore(); }

  //time(s: string) { return new Date(s).toLocaleString(); }
  imgUrl(id: number) { return this.imageUrls.get(id) || ""; }
  date(s: string) { return new Date(s).toLocaleDateString(); }

  private sameDay(a: string, b: string) {
    const da = new Date(a); const db = new Date(b);
    return da.getFullYear() === db.getFullYear() &&
           da.getMonth() === db.getMonth() &&
           da.getDate() === db.getDate();
  }
  showDateSeparator(i: number) {
    if (i === 0) return true;
    return !this.sameDay(this.items[i - 1].createdAtUtc, this.items[i].createdAtUtc);
  }

  openDialog(item: MealListItem) {
    const ref = this.dialog.open(HistoryDetailDialogComponent, {
      data: { item, imageUrl: this.imgUrl(item.id) },
      maxWidth: "100vw",
      maxHeight: "100vh",
      width: "95vw",
      height: "95vh"
    });
    ref.afterClosed().subscribe(r => {
      if (r?.deleted) {
        this.items = this.items.filter(x => x.id !== item.id);
        this.total = Math.max(0, this.total - 1);
        this.recomputeDateTotals();
        this.loadMore();
      }
    });
  }

  private dateKey(s: string): string {
    const d = new Date(s);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  }

  private recomputeDateTotals() {
    this.dateTotals.clear();
    for (const m of this.items) {
      const key = this.dateKey(m.createdAtUtc);
      const kcal = Number(m.caloriesKcal ?? 0);
      this.dateTotals.set(key, (this.dateTotals.get(key) ?? 0) + (isNaN(kcal) ? 0 : kcal));
    }
  }

  dateTotalFor(dateStr: string): number {
    return this.dateTotals.get(this.dateKey(dateStr)) ?? 0;
  }

  /* --- раскрытие ингредиентов --- */
  toggleIng(m: MealListItem) {
    if (this.ingOpen.has(m.id)) this.ingOpen.delete(m.id);
    else this.ingOpen.add(m.id);
  }
  isIngOpen(m: MealListItem) {
    return this.ingOpen.has(m.id);
  }

  time(s: string) {
  return new Date(s).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

}
