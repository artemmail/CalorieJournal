import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { InfiniteScrollModule } from "ngx-infinite-scroll";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem, PersonalCard } from "../../services/foodbot-api.types";
import { HistoryDetailDialogComponent } from "./history-detail.dialog";
import { HistoryUpdatesService } from "../../services/history-updates.service";
import { Subscription } from "rxjs";
import { FoodBotAuthLinkService } from "../../services/foodbot-auth-link.service";
import { Router } from "@angular/router";

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
  profileComplete = false;

  imageUrls = new Map<number, string>();
  private dateTotals = new Map<string, number>();
  private updatesSub?: Subscription;
  private pendingPollTimer?: ReturnType<typeof setTimeout>;
  private pendingPollInFlight = false;
  private pendingCursor = "";
  private pendingNoChangeStreak = 0;
  private readonly pendingPollMs = 30_000;
  private readonly pendingPollBackoffMs = 60_000;

  /** локальное состояние раскрытых ингредиентов */
  private ingOpen = new Set<number>();

  constructor(
    private api: FoodbotApiService,
    private auth: FoodBotAuthLinkService,
    private snack: MatSnackBar,
    private dialog: MatDialog,
    private updates: HistoryUpdatesService,
    private router: Router
  ) {}

  get isAnonymousMode(): boolean {
    return this.auth.isAnonymousAccount;
  }

  ngOnInit() {
    this.loadMore();
    this.loadProfileStatus();

    this.updatesSub = this.updates.updates().subscribe(item => this.applyRealtimeUpdate(item));
  }

  ngOnDestroy() {
    this.stopPendingPolling();
    this.clearAllImageUrls();
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
            this.api.getMealImageObjectUrl(m.id).subscribe((url) => this.setImageUrl(m.id, url));
          }
        }
        this.recomputeDateTotals();
        this.updatePendingPolling();
        this.loading = false;
      },
      error: () => {
        this.snack.open("Не удалось загрузить историю", "OK", { duration: 2000 });
        this.loading = false;
      }
    });
  }

  onScrollDown() { this.loadMore(); }
  goProfile() { void this.router.navigateByUrl("/profile"); }
  goGuide() { void this.router.navigateByUrl("/guide"); }

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
    if (item.pendingRequestId) {
      this.snack.open("Запрос ещё обрабатывается", "OK", { duration: 1500 });
      return;
    }

    const ref = this.dialog.open(HistoryDetailDialogComponent, {
      data: { item, imageUrl: this.imgUrl(item.id) },
      maxWidth: "100vw",
      maxHeight: "100vh",
      width: "95vw",
      height: "95vh"
    });
    ref.afterClosed().subscribe(r => {
      if (r?.deleted) {
        this.clearImageUrl(item.id);
        this.items = this.items.filter(x => x.id !== item.id);
        this.total = Math.max(0, this.total - 1);
        this.recomputeDateTotals();
        this.updatePendingPolling();
        this.loadMore();
      }
    });
  }

  queueLabel(item: MealListItem): string {
    return item.pendingRequestId ? "В обработке…" : "Обновляется…";
  }

  private applyRealtimeUpdate(item: MealListItem) {
    let replacedPending = 0;
    if (item.replacesPendingRequestId) {
      const toRemove = this.items.filter(x => x.pendingRequestId === item.replacesPendingRequestId);
      for (const removed of toRemove) {
        this.clearImageUrl(removed.id);
      }
      replacedPending = toRemove.length;
      this.items = this.items.filter(x => x.pendingRequestId !== item.replacesPendingRequestId);
    }

    const idx = this.items.findIndex(x => x.id === item.id);
    if (idx >= 0) {
      this.items[idx] = item;
    } else {
      this.items = [item, ...this.items];
      if (replacedPending === 0) {
        this.total++;
      }
    }

    this.items.sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());
    if (item.hasImage) {
      this.api.getMealImageObjectUrl(item.id).subscribe(url => this.setImageUrl(item.id, url));
    } else if (item.id !== 0) {
      this.clearImageUrl(item.id);
    }
    this.recomputeDateTotals();
    this.updatePendingPolling();
  }

  private setImageUrl(id: number, url: string) {
    const prev = this.imageUrls.get(id);
    if (prev && prev !== url) {
      URL.revokeObjectURL(prev);
    }
    this.imageUrls.set(id, url);
  }

  private clearImageUrl(id: number) {
    const prev = this.imageUrls.get(id);
    if (!prev) return;
    URL.revokeObjectURL(prev);
    this.imageUrls.delete(id);
  }

  private clearAllImageUrls() {
    for (const url of this.imageUrls.values()) {
      URL.revokeObjectURL(url);
    }
    this.imageUrls.clear();
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
    return new Date(s).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  }

  private loadProfileStatus() {
    this.api.getPersonalCard().subscribe({
      next: (card) => {
        this.profileComplete = this.isProfileComplete(card);
      },
      error: () => {
        this.profileComplete = false;
      }
    });
  }

  private isProfileComplete(card: PersonalCard | null): card is PersonalCard {
    if (!card) return false;
    const required: Array<keyof PersonalCard> = ["gender", "birthYear", "heightCm", "weightKg", "activityLevel"];
    return required.every(field => card[field] !== null && card[field] !== undefined);
  }

  private hasPendingItems(): boolean {
    return this.items.some(x => x.isProcessing);
  }

  private updatePendingPolling() {
    if (!this.hasPendingItems()) {
      this.stopPendingPolling();
      this.pendingNoChangeStreak = 0;
      return;
    }
    this.schedulePendingPoll();
  }

  private schedulePendingPoll(delayMs?: number) {
    if (this.pendingPollTimer || this.pendingPollInFlight) return;
    if (!this.hasPendingItems()) return;

    const baseDelay = delayMs ?? this.nextPendingDelayMs();
    const delayWithJitter = this.withJitter(baseDelay);
    this.pendingPollTimer = setTimeout(() => {
      this.pendingPollTimer = undefined;
      void this.runPendingPoll();
    }, delayWithJitter);
  }

  private stopPendingPolling() {
    if (!this.pendingPollTimer) return;
    clearTimeout(this.pendingPollTimer);
    this.pendingPollTimer = undefined;
  }

  private nextPendingDelayMs(): number {
    return this.pendingNoChangeStreak >= 3 ? this.pendingPollBackoffMs : this.pendingPollMs;
  }

  private withJitter(ms: number): number {
    const jitter = Math.floor(Math.random() * 6001) - 3000; // -3..+3 sec
    return Math.max(5_000, ms + jitter);
  }

  private runPendingPoll(): Promise<void> {
    if (this.pendingPollInFlight || !this.hasPendingItems()) {
      return Promise.resolve();
    }

    this.pendingPollInFlight = true;
    let shouldScheduleNext = false;
    let nextDelay: number | undefined;

    return new Promise((resolve) => {
      this.api.getMealsPendingState(this.pendingCursor || undefined).subscribe({
        next: (state) => {
          this.pendingCursor = state.cursor;

          if (state.changed) {
            this.pendingNoChangeStreak = 0;
            this.reloadVisibleItems();
          } else {
            this.pendingNoChangeStreak++;
          }

          if (!state.hasPending) {
            this.stopPendingPolling();
            if (this.hasPendingItems()) {
              this.reloadVisibleItems();
            }
            return;
          }

          shouldScheduleNext = true;
        },
        error: () => {
          this.pendingNoChangeStreak = 0;
          shouldScheduleNext = true;
          nextDelay = this.pendingPollBackoffMs;
          this.pendingPollInFlight = false;
          this.schedulePendingPoll(nextDelay);
          resolve();
        },
        complete: () => {
          this.pendingPollInFlight = false;
          if (shouldScheduleNext) {
            this.schedulePendingPoll(nextDelay);
          }
          resolve();
        }
      });
    });
  }

  private reloadVisibleItems() {
    if (this.loading) return;

    const limit = Math.max(this.pageSize, this.items.length || this.pageSize);
    this.api.getMeals(limit, 0).subscribe({
      next: (res) => {
        this.clearAllImageUrls();
        this.items = res.items;
        this.total = res.total;
        for (const m of this.items) {
          if (m.hasImage) {
            this.api.getMealImageObjectUrl(m.id).subscribe((url) => this.setImageUrl(m.id, url));
          }
        }
        this.recomputeDateTotals();
        this.updatePendingPolling();
      },
      error: () => {
        this.schedulePendingPoll(this.pendingPollBackoffMs);
      }
    });
  }

}
