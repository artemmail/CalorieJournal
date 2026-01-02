import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { Router } from "@angular/router";
import { FoodBotAuthLinkService, ExchangeStartCodeResponse } from "../../services/foodbot-auth-link.service";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTabsModule } from "@angular/material/tabs";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { showErrorAlert } from "../../utils/alerts";

@Component({
  selector: "app-auth",
  standalone: true,
    imports: [
      CommonModule,
      FormsModule,
      MatCardModule,
      MatButtonModule,
      MatIconModule,
      MatSnackBarModule,
      MatProgressSpinnerModule,
      MatTabsModule,
      MatFormFieldModule,
      MatInputModule
    ],
  templateUrl: "./auth.page.html",
  styleUrls: ["./auth.page.scss"]
})
export class AuthPage implements OnInit, OnDestroy {
  code = "";
  expiresAt = "";
  busy = false;
  errorMsg = "";
  externalBusy = false;
  externalError = "";
  externalId = "";
  externalUsername = "";
  flow?: Awaited<ReturnType<FoodBotAuthLinkService["startLoginFlow"]>>;
  private autoRefreshInterval?: ReturnType<typeof setInterval>;
  private autoRefreshTimeout?: ReturnType<typeof setTimeout>;

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

  ngOnDestroy() { this.stopAutoRefresh(); }

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
    this.startAutoRefresh();
  }

  async openBotWithFreshCode() {
    if (this.busy) return;
    this.busy = true;
    try {
      await this.startFlow();
      this.openBot();
    } catch (e) {
      this.errorMsg = "Не удалось получить код. Проверьте доступность API/CORS.";
      showErrorAlert(e, "Не удалось получить свежий код");
    } finally {
      this.busy = false;
    }
  }

  async refresh() {
    if (!this.flow) return;
    this.busy = true;
    try {
      const resp: ExchangeStartCodeResponse = await this.flow.waitForJwt();
      this.auth.setToken(resp);
      this.stopAutoRefresh();
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
      this.startAutoRefresh();
    } catch (e) {
      this.errorMsg = "Не удалось получить новый код.";
      showErrorAlert(e, "Ошибка при запросе нового кода");
    }
  }

  async loginExternal() {
    if (this.externalBusy) return;
    this.externalBusy = true;
    this.externalError = "";
    try {
      const back = window.location.origin + "/auth";
      this.auth.openVk(back, false);
    } catch (e) {
      this.externalBusy = false;
      this.externalError = "Не удалось открыть VK.";
      showErrorAlert(e, "Ошибка VK");
    }
  }

  logout() {
    this.auth.logout();
    this.snack.open("Вы вышли", "OK", { duration: 1000 });
  }

  private startAutoRefresh() {
    this.stopAutoRefresh();
    this.autoRefreshInterval = setInterval(() => {
      if (!this.busy) this.refresh();
    }, 5000);
    this.autoRefreshTimeout = setTimeout(() => this.stopAutoRefresh(), 3 * 60 * 1000);
  }

  private stopAutoRefresh() {
    if (this.autoRefreshInterval) {
      clearInterval(this.autoRefreshInterval);
      this.autoRefreshInterval = undefined;
    }
    if (this.autoRefreshTimeout) {
      clearTimeout(this.autoRefreshTimeout);
      this.autoRefreshTimeout = undefined;
    }
  }
}
