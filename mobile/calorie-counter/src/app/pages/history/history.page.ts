import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { InfiniteScrollModule } from 'ngx-infinite-scroll';
import { Router } from "@angular/router";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem } from "../../services/foodbot-api.types";
import { FoodBotAuthLinkService } from "../../services/foodbot-auth-link.service";
import { HistoryDetailDialogComponent } from "./history-detail.dialog";

@Component({
  selector: "app-history",
  standalone: true,
  imports: [CommonModule, MatCardModule, InfiniteScrollModule, MatSnackBarModule, MatDialogModule, MatProgressSpinnerModule],
  templateUrl: "./history.page.html",
  styleUrls: ["./history.page.scss"]
})
export class HistoryPage implements OnInit, OnDestroy {
    items: MealListItem[] = [];
    total = 0;
    pageSize = 10;
    loading = false;

  imageUrls = new Map<number, string>();

  constructor(
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private router: Router,
    private auth: FoodBotAuthLinkService,
    private dialog: MatDialog
  ) {}

    ngOnInit() {
      if (!this.auth.isAuthenticated()) { this.router.navigateByUrl("/auth"); return; }
      this.loadMore();
    }

  ngOnDestroy() {
    // чистим ObjectURL'ы
    for (const url of this.imageUrls.values()) URL.revokeObjectURL(url);
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
              this.api.getMealImageObjectUrl(m.id).subscribe(url => this.imageUrls.set(m.id, url));
            }
          }
          this.loading = false;
        },
        error: () => {
          this.snack.open("Не удалось загрузить историю", "OK", { duration: 2000 });
          this.loading = false;
        }
      });
    }

    onScrollDown() { this.loadMore(); }

  time(s: string) { return new Date(s).toLocaleString(); }
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
    this.dialog.open(HistoryDetailDialogComponent, {
      data: { item, imageUrl: this.imgUrl(item.id) },
      maxWidth: "100vw",
      maxHeight: "100vh",
      width: "95vw",
      height: "95vh"
    });
  }
}


