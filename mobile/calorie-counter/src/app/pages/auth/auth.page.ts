import { Component, OnDestroy, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { Router } from "@angular/router";
import { HttpErrorResponse } from "@angular/common/http";
import {
  FoodBotAuthLinkService,
  ExchangeStartCodeResponse,
  SessionBootstrapError
} from "../../services/foodbot-auth-link.service";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { showErrorAlert } from "../../utils/alerts";

@Component({
  selector: "app-auth",
  standalone: true,
    imports: [
      CommonModule,
      MatCardModule,
      MatButtonModule,
      MatIconModule,
      MatSnackBarModule,
      MatProgressSpinnerModule
    ],
  templateUrl: "./auth.page.html",
  styleUrls: ["./auth.page.scss"]
})
export class AuthPage implements OnInit, OnDestroy {
  code = "";
  expiresAt = "";
  busy = false;
  errorMsg = "";
  alreadyLinked = false;
  canContinueWithoutTelegram = false;
  flow?: Awaited<ReturnType<FoodBotAuthLinkService["startLoginFlow"]>>;
  private autoRefreshInterval?: ReturnType<typeof setInterval>;
  private autoRefreshTimeout?: ReturnType<typeof setTimeout>;

  constructor(
    private auth: FoodBotAuthLinkService,
    private snack: MatSnackBar,
    private router: Router
  ) {}

  get currentChatId(): number | null {
    return this.auth.chatId;
  }

  get webVersionUrl(): string {
    return this.auth.webAppUrl;
  }

  async ngOnInit() {
    await this.initialize();
  }

  ngOnDestroy() { this.stopAutoRefresh(); }

  async retryStartup() {
    if (this.busy) return;
    this.stopAutoRefresh();
    this.code = "";
    this.expiresAt = "";
    this.flow = undefined;
    await this.initialize();
  }

  openWebVersion() {
    window.open(this.webVersionUrl, "_blank", "noopener,noreferrer");
  }

  async continueWithoutTelegram() {
    if (this.busy) return;
    this.busy = true;
    try {
      if (!this.auth.isAuthenticated()) {
        await this.auth.ensureSession();
      }
      await this.router.navigateByUrl("/history");
    } catch (e) {
      this.errorMsg = this.describeSessionStartError(e);
    } finally {
      this.busy = false;
    }
  }

  private async startFlow() {
    this.errorMsg = "";
    this.flow = await this.auth.startLoginFlow({
      onCode: (code, exp) => { this.code = code; this.expiresAt = exp; }
    });
    this.canContinueWithoutTelegram = this.auth.isAuthenticated();
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
      this.errorMsg = this.describeFlowError(e);
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
      const terminalMsg = this.getTerminalExchangeMessage(e);
      if (terminalMsg) {
        this.stopAutoRefresh();
        this.errorMsg = terminalMsg;
        showErrorAlert(e, terminalMsg);
      } else {
        this.snack.open("Пока ожидаем привязку кода в боте…", "OK", { duration: 1500 });
      }
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
      this.errorMsg = this.describeFlowError(e);
      showErrorAlert(e, "Ошибка при запросе нового кода");
    }
  }

  async logout() {
    this.stopAutoRefresh();
    this.auth.logout();
    this.alreadyLinked = false;
    this.code = "";
    this.expiresAt = "";
    this.flow = undefined;
    this.errorMsg = "";
    this.snack.open("Вы вышли", "OK", { duration: 1000 });

    try {
      await this.auth.ensureSession();
      await this.startFlow();
    } catch (e) {
      this.errorMsg = this.describeSessionStartError(e);
      showErrorAlert(e, "Ошибка после выхода");
    }
  }

  goHistory() {
    this.router.navigateByUrl("/history");
  }

  async relinkTelegram() {
    this.stopAutoRefresh();
    this.alreadyLinked = false;
    this.code = "";
    this.expiresAt = "";
    this.flow = undefined;
    this.errorMsg = "";
    try {
      await this.startFlow();
    } catch (e) {
      this.errorMsg = this.describeFlowError(e);
      showErrorAlert(e, "Не удалось начать перепривязку");
    }
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

  private async initialize() {
    this.errorMsg = "";
    this.busy = true;
    try {
      await this.auth.ensureSession();
      this.canContinueWithoutTelegram = this.auth.isAuthenticated();
      this.alreadyLinked = this.auth.isAuthenticated() && !this.auth.isAnonymousAccount;
      if (this.alreadyLinked) return;
      await this.startFlow();
    } catch (e) {
      this.canContinueWithoutTelegram = this.auth.isAuthenticated();
      this.errorMsg = this.describeSessionStartError(e);
      if (!(e instanceof SessionBootstrapError)) {
        showErrorAlert(e, "Не удалось подготовить вход");
      }
    } finally {
      this.busy = false;
    }
  }

  private describeSessionStartError(err: unknown): string {
    if (err instanceof SessionBootstrapError) {
      switch (err.kind) {
        case "timeout":
          return "Сервер долго не отвечает. Проверьте интернет и повторите.";
        case "network":
          return "Нет соединения с сервером. Проверьте сеть или VPN и повторите.";
        case "tls":
          return "Не удалось установить защищённое соединение (TLS/сертификат). Попробуйте другую сеть или веб-версию.";
        case "api":
          return "Сервис временно недоступен. Повторите чуть позже.";
        default:
          return "Не удалось запустить сессию. Повторите попытку.";
      }
    }
    return this.describeFlowError(err);
  }

  private describeFlowError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        return "Не удалось связаться с сервером. Telegram можно подключить позже.";
      }
      if (err.status >= 500) {
        return "Сервер временно недоступен. Telegram можно подключить позже.";
      }
      return `Ошибка при получении кода входа (HTTP ${err.status}).`;
    }
    if (err instanceof Error && err.message) {
      return err.message;
    }
    return "Не удалось получить код входа. Telegram можно подключить позже.";
  }

  private getTerminalExchangeMessage(err: unknown): string | null {
    if (!(err instanceof HttpErrorResponse)) return null;
    const code = err.error?.error;
    if (typeof code !== "string") return null;

    switch (code) {
      case "expired":
        return "Код истёк. Запросите новый и повторите вход.";
      case "not_found":
        return "Код не найден. Проверьте, что отправили его правильному боту.";
      case "already_used":
        return "Код уже использован. Запросите новый код.";
      case "identity_conflict":
        return "Этот Telegram уже привязан к другому аккаунту.";
      default:
        return "Ошибка привязки кода. Попробуйте запросить новый код.";
    }
  }
}
