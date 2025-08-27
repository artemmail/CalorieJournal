import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatDialogModule } from "@angular/material/dialog";
import { Router } from "@angular/router";
import { FoodbotApiService } from "../../services/foodbot-api.service";
import { MealListItem, ClarifyResult } from "../../services/foodbot-api.types";
import { VoiceService } from "../../services/voice.service";
import { FoodBotAuthLinkService } from "../../services/foodbot-auth-link.service";

@Component({
  selector: "app-history",
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatPaginatorModule, MatSnackBarModule, MatDialogModule],
  templateUrl: "./history.page.html",
  styleUrls: ["./history.page.scss"]
})
export class HistoryPage implements OnInit, OnDestroy {
  items: MealListItem[] = [];
  total = 0;
  pageSize = 10;
  pageIndex = 0;

  imageUrls = new Map<number, string>();

  constructor(
    private api: FoodbotApiService,
    private voice: VoiceService,
    private snack: MatSnackBar,
    private router: Router,
    private auth: FoodBotAuthLinkService
  ) {}

  ngOnInit() {
    if (!this.auth.isAuthenticated()) { this.router.navigateByUrl("/auth"); return; }
    this.loadPage(0);
  }

  ngOnDestroy() {
    // чистим ObjectURL'ы
    for (const url of this.imageUrls.values()) URL.revokeObjectURL(url);
  }

  loadPage(index: number) {
    const offset = index * this.pageSize;
    this.api.getMeals(this.pageSize, offset).subscribe({
      next: (res) => {
        this.items = res.items;
        this.total = res.total;
        this.pageIndex = index;
        // загружаем превью
        this.imageUrls.forEach(u => URL.revokeObjectURL(u));
        this.imageUrls.clear();
        for (const m of this.items) {
          if (m.hasImage) {
            this.api.getMealImageObjectUrl(m.id).subscribe(url => this.imageUrls.set(m.id, url));
          }
        }
      },
      error: () => this.snack.open("Не удалось загрузить историю", "OK", { duration: 2000 })
    });
  }

  pageChange(e: PageEvent) { this.pageSize = e.pageSize; this.loadPage(e.pageIndex); }

  async clarifyVoice(item: MealListItem) {
    try {
      const text = await this.voice.listenOnce("ru-RU");
      if (!text) return;
      this.api.clarifyText(item.id, text).subscribe({
        next: (r: ClarifyResult) => {
          item.dishName = r.result.dish;
          item.caloriesKcal = r.result.calories_kcal;
          item.proteinsG = r.result.proteins_g;
          item.fatsG = r.result.fats_g;
          item.carbsG = r.result.carbs_g;
          item.weightG = r.result.weight_g;
          item.ingredients = r.result.ingredients;
          this.snack.open("Уточнение применено", "OK", { duration: 1500 });
        },
        error: () => this.snack.open("Ошибка уточнения", "OK", { duration: 1500 })
      });
    } catch {
      this.snack.open("Речь не распознана", "OK", { duration: 1500 });
    }
  }

  clarifyText(item: MealListItem) {
    const note = prompt("Введите уточнение по блюду/массе/ингредиентам:", "");
    if (!note) return;
    this.api.clarifyText(item.id, note).subscribe({
      next: (r: ClarifyResult) => {
        item.dishName = r.result.dish;
        item.caloriesKcal = r.result.calories_kcal;
        item.proteinsG = r.result.proteins_g;
        item.fatsG = r.result.fats_g;
        item.carbsG = r.result.carbs_g;
        item.weightG = r.result.weight_g;
        item.ingredients = r.result.ingredients;
        this.snack.open("Уточнение применено", "OK", { duration: 1500 });
      },
      error: () => this.snack.open("Ошибка уточнения", "OK", { duration: 1500 })
    });
  }

  time(s: string) { return new Date(s).toLocaleString(); }
  imgUrl(id: number) { return this.imageUrls.get(id) || ""; }
}


