import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { Router } from "@angular/router";
import { FoodBotAuthLinkService, ExchangeStartCodeResponse } from "../../services/foodbot-auth-link.service";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { showErrorAlert } from "../../utils/alerts";

@Component({
  selector: "app-auth",
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule],
  templateUrl: "./auth.page.html",
  styleUrls: ["./auth.page.scss"]
})
export class AuthPage implements OnInit, OnDestroy {
  code = "";
  expiresAt = "";
  busy = false;
  errorMsg = "";
  flow?: Awaited<ReturnType<FoodBotAuthLinkService["startLoginFlow"]>>;

  constructor(
    private auth: FoodBotAuthLinkService,
    private snack: MatSnackBar,
    private router: Router
  ) {}

  async ngOnInit() {
    try {
      await this.startFlow();
    } catch (e) {
      this.errorMsg = "Не удалось получить код. Проверьте доступность API/CORS.";
      showErrorAlert(e, "Не удалось получить код");
    }
  }

  ngOnDestroy() {}

  private async startFlow() {
    this.errorMsg = "";
    this.flow = await this.auth.startLoginFlow({
      onCode: (code, exp) => { this.code = code; this.expiresAt = exp; }
    });
  }

  async copyCode() {
    if (!this.code) return;
    try {
      await navigator.clipboard.writeText(this.code);
      this.snack.open("Код скопирован", "OK", { duration: 1200 });
    } catch (e) {
      showErrorAlert(e, "Не удалось скопировать код");
    }
  }

  openBot() {
    try { this.flow?.openBot(); }
    catch (e) { showErrorAlert(e, "Не удалось открыть Telegram"); }
  }

  async refresh() {
    if (!this.flow) return;
    this.busy = true;
    try {
      const resp: ExchangeStartCodeResponse = await this.flow.waitForJwt();
      this.auth.setToken(resp);
      this.snack.open("Вход выполнен", "OK", { duration: 1200 });
      this.router.navigateByUrl("/history");
    } catch (e) {
      showErrorAlert(e, "Ещё нет привязки кода");
      this.snack.open("Пока ожидаем привязку кода в боте…", "OK", { duration: 1500 });
    } finally {
      this.busy = false;
    }
  }

  async restart() {
    this.code = ""; this.expiresAt = ""; this.flow = undefined;
    try {
      await this.startFlow();
    } catch (e) {
      this.errorMsg = "Не удалось получить новый код.";
      showErrorAlert(e, "Ошибка при запросе нового кода");
    }
  }

  logout() {
    this.auth.logout();
    this.snack.open("Вы вышли", "OK", { duration: 1000 });
  }
}
