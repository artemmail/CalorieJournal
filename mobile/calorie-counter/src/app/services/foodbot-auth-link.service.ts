/**
 * FoodBotAuthLinkService — bootstrap anonymous account and optional provider linking.
 */
import { Injectable } from "@angular/core";
import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from "@angular/common/http";
import { Capacitor } from "@capacitor/core";
import { Observable, TimeoutError, firstValueFrom, of, Subject, throwError, timer } from "rxjs";
import { catchError, exhaustMap, filter, map, take, timeout } from "rxjs/operators";

declare const environment: any;

export interface AuthSessionResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresInSeconds: number;
  refreshExpiresInSeconds: number;
  appUserId: number;
  chatId: number;
  isAnonymous: boolean;
}

export interface RequestCodeResponse { code: string; expiresAtUtc: string; }
export interface StatusResponse { linked: boolean; expiresAtUtc: string; secondsLeft: number; }
export interface ExchangeStartCodeResponse extends AuthSessionResponse {}
export interface ExchangePending { status: "pending"; }
export type SessionBootstrapErrorKind = "network" | "tls" | "timeout" | "api" | "unknown";

export class SessionBootstrapError extends Error {
  constructor(
    readonly kind: SessionBootstrapErrorKind,
    message: string,
    readonly status?: number,
    readonly originalError?: unknown
  ) {
    super(message);
    this.name = "SessionBootstrapError";
  }
}

const JWT_KEY = "foodbot.jwt";
const JWT_EXP = "foodbot.jwt.exp";
const REFRESH_KEY = "foodbot.refresh";
const REFRESH_EXP = "foodbot.refresh.exp";
const APP_USER_ID_KEY = "foodbot.app_user_id";
const CHAT_ID_KEY = "foodbot.chat_id";
const IS_ANON_KEY = "foodbot.is_anonymous";
const INSTALL_ID_KEY = "foodbot.install_id";
const MOBILE_DEFAULT_API_BASE_URL = "https://healthymeals.space";
const DEFAULT_BOT_USERNAME = "CalorieJournal_bot";
const ANON_START_TIMEOUT_MS = 9000;
const ANON_START_MAX_ATTEMPTS = 3;
const ANON_START_RETRY_DELAYS_MS = [800, 1700];

@Injectable({ providedIn: "root" })
export class FoodBotAuthLinkService {
  constructor(private http: HttpClient) {}

  private parseJwtExpMs(token: string): number | null {
    try {
      const parts = token.split(".");
      if (parts.length < 2) return null;

      const payloadBase64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const pad = "=".repeat((4 - (payloadBase64.length % 4)) % 4);
      const json = atob(payloadBase64 + pad);
      const payload = JSON.parse(json) as { exp?: number };

      if (!payload?.exp || !Number.isFinite(payload.exp)) return null;
      return payload.exp * 1000;
    } catch {
      return null;
    }
  }

  private tokenChangedSubj = new Subject<string | null>();
  tokenChanges(): Observable<string | null> { return this.tokenChangedSubj.asObservable(); }

  private get apiBase(): string {
    const envApiBase = this.normalizeBaseUrl((window as any).environment?.apiBaseUrl ?? environment?.apiBaseUrl ?? "");

    if (this.shouldUseMobileDefaultApi(envApiBase)) {
      return MOBILE_DEFAULT_API_BASE_URL;
    }

    if (envApiBase) {
      return envApiBase;
    }

    if (this.isNativeApp()) {
      return MOBILE_DEFAULT_API_BASE_URL;
    }

    return this.normalizeBaseUrl(window.location.origin) || MOBILE_DEFAULT_API_BASE_URL;
  }
  get apiBaseUrl(): string { return this.apiBase; }
  get webAppUrl(): string {
    try {
      const url = new URL(this.apiBase);
      return `${url.protocol}//${url.host}`;
    } catch {
      return MOBILE_DEFAULT_API_BASE_URL;
    }
  }

  private shouldUseMobileDefaultApi(apiBase: string): boolean {
    return this.isNativeApp() && this.isLocalhostLike(apiBase);
  }

  private isNativeApp(): boolean {
    try {
      return Capacitor.isNativePlatform();
    } catch {
      return false;
    }
  }

  private isLocalhostLike(value: string): boolean {
    if (!value) return false;

    try {
      const host = new URL(value).hostname.toLowerCase();
      return host === "localhost" || host === "127.0.0.1" || host === "::1";
    } catch {
      const normalized = value.toLowerCase();
      return normalized.includes("localhost") || normalized.includes("127.0.0.1");
    }
  }

  private normalizeBaseUrl(value: string): string {
    const trimmed = (value ?? "").trim();
    if (!trimmed) return "";
    return trimmed.replace(/\/+$/, "");
  }

  private get botUsername(): string {
    return (window as any).environment?.telegramBot ?? environment?.telegramBot ?? "your_bot_username";
  }

  private normalizeBotUsername(raw: string | null | undefined): string {
    let value = (raw ?? "").trim();
    if (!value) return DEFAULT_BOT_USERNAME;

    if (value.startsWith("tg://")) {
      try {
        const url = new URL(value);
        const domain = url.searchParams.get("domain")?.trim();
        if (domain) value = domain;
      } catch {
        const match = value.match(/domain=([^&\s]+)/i);
        if (match?.[1]) value = match[1];
      }
    } else if (/^https?:\/\//i.test(value)) {
      try {
        const url = new URL(value);
        if (/^(?:www\.)?(?:t\.me|telegram\.me)$/i.test(url.hostname)) {
          const segment = decodeURIComponent(url.pathname).split("/").filter(Boolean)[0];
          if (segment) value = segment;
        }
      } catch {
        // Ignore and continue with the raw value.
      }
    }

    value = value.replace(/^@+/, "").trim();
    const cleaned = value.replace(/[^A-Za-z0-9_]/g, "");
    return cleaned || DEFAULT_BOT_USERNAME;
  }

  get installId(): string {
    let id = localStorage.getItem(INSTALL_ID_KEY);
    if (id) return id;

    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      id = crypto.randomUUID();
    } else {
      id = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    }

    localStorage.setItem(INSTALL_ID_KEY, id);
    return id;
  }

  get token(): string | null {
    const t = localStorage.getItem(JWT_KEY);
    if (!t) return null;

    let exp = Number(localStorage.getItem(JWT_EXP) ?? 0);
    if (!exp) {
      const jwtExpMs = this.parseJwtExpMs(t);
      if (!jwtExpMs) {
        localStorage.removeItem(JWT_KEY);
        localStorage.removeItem(JWT_EXP);
        return null;
      }
      exp = jwtExpMs;
      localStorage.setItem(JWT_EXP, String(exp));
    }

    if (exp && Date.now() > exp) {
      localStorage.removeItem(JWT_KEY);
      localStorage.removeItem(JWT_EXP);
      return null;
    }
    return t;
  }

  get refreshToken(): string | null {
    const t = localStorage.getItem(REFRESH_KEY);
    const exp = Number(localStorage.getItem(REFRESH_EXP) ?? 0);
    if (!t) return null;
    if (!exp) {
      localStorage.removeItem(REFRESH_KEY);
      localStorage.removeItem(REFRESH_EXP);
      return null;
    }
    if (exp && Date.now() > exp) {
      localStorage.removeItem(REFRESH_KEY);
      localStorage.removeItem(REFRESH_EXP);
      return null;
    }
    return t;
  }

  get appUserId(): number | null {
    const raw = localStorage.getItem(APP_USER_ID_KEY);
    if (!raw) return null;
    const id = Number(raw);
    return Number.isFinite(id) ? id : null;
  }

  get chatId(): number | null {
    const raw = localStorage.getItem(CHAT_ID_KEY);
    if (!raw) return null;
    const id = Number(raw);
    return Number.isFinite(id) ? id : null;
  }

  get isAnonymousAccount(): boolean {
    return localStorage.getItem(IS_ANON_KEY) === "1";
  }

  isAuthenticated(): boolean { return !!this.token; }

  authHeaders(): HttpHeaders {
    const t = this.token;
    return new HttpHeaders(t ? { Authorization: `Bearer ${t}` } : {});
  }

  setToken(resp: AuthSessionResponse) {
    const now = Date.now();
    const accessExpMs = now + resp.expiresInSeconds * 1000;
    const refreshExpMs = now + resp.refreshExpiresInSeconds * 1000;

    localStorage.setItem(JWT_KEY, resp.accessToken);
    localStorage.setItem(JWT_EXP, String(accessExpMs));
    localStorage.setItem(REFRESH_KEY, resp.refreshToken);
    localStorage.setItem(REFRESH_EXP, String(refreshExpMs));
    localStorage.setItem(APP_USER_ID_KEY, String(resp.appUserId));
    localStorage.setItem(CHAT_ID_KEY, String(resp.chatId));
    localStorage.setItem(IS_ANON_KEY, resp.isAnonymous ? "1" : "0");

    this.tokenChangedSubj.next(resp.accessToken);
  }

  logout() {
    localStorage.removeItem(JWT_KEY);
    localStorage.removeItem(JWT_EXP);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(REFRESH_EXP);
    localStorage.removeItem(APP_USER_ID_KEY);
    localStorage.removeItem(CHAT_ID_KEY);
    localStorage.removeItem(IS_ANON_KEY);
    this.tokenChangedSubj.next(null);
  }

  startAnonymousSession(device = "Android-Ionic"): Observable<AuthSessionResponse> {
    return this.http.post<AuthSessionResponse>(`${this.apiBase}/api/auth/anonymous/start`, {
      installId: this.installId,
      device
    });
  }

  refreshSession(token: string, device = "Android-Ionic"): Observable<AuthSessionResponse> {
    return this.http.post<AuthSessionResponse>(`${this.apiBase}/api/auth/refresh`, {
      refreshToken: token,
      installId: this.installId,
      device
    });
  }

  async ensureSession(device = "Android-Ionic"): Promise<void> {
    if (this.token) return;

    const rt = this.refreshToken;
    if (rt) {
      try {
        const refreshed = await firstValueFrom(this.refreshSession(rt, device));
        this.setToken(refreshed);
        return;
      } catch {
        this.logout();
      }
    }

    await this.pingAuthHealthForDiagnostics();
    const anon = await this.startAnonymousSessionWithRetry(device);
    this.setToken(anon);
  }

  private async startAnonymousSessionWithRetry(device: string): Promise<AuthSessionResponse> {
    let lastError: SessionBootstrapError | null = null;

    for (let attempt = 1; attempt <= ANON_START_MAX_ATTEMPTS; attempt++) {
      try {
        return await firstValueFrom(
          this.startAnonymousSession(device).pipe(timeout({ first: ANON_START_TIMEOUT_MS }))
        );
      } catch (err) {
        const classified = this.classifyAnonymousStartError(err);
        lastError = classified;
        this.logAnonymousStartError(classified, attempt);

        const canRetry = this.shouldRetryAnonymousStart(classified) && attempt < ANON_START_MAX_ATTEMPTS;
        if (!canRetry) break;

        await this.sleep(this.getAnonymousRetryDelayMs(attempt));
      }
    }

    throw lastError ?? new SessionBootstrapError("unknown", "Не удалось запустить анонимную сессию.");
  }

  private async pingAuthHealthForDiagnostics(): Promise<void> {
    try {
      await firstValueFrom(
        this.http
          .get<{ status: string }>(`${this.apiBase}/api/auth/health`)
          .pipe(timeout({ first: 3000 }))
      );
    } catch (err) {
      const classified = this.classifyAnonymousStartError(err);
      this.logAnonymousStartError(classified, 0);
    }
  }

  private classifyAnonymousStartError(err: unknown): SessionBootstrapError {
    if (err instanceof SessionBootstrapError) return err;

    if (err instanceof TimeoutError) {
      return new SessionBootstrapError("timeout", "Сервер не ответил вовремя.");
    }

    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        if (this.looksLikeTlsError(err)) {
          return new SessionBootstrapError("tls", "Ошибка защищённого соединения (TLS/сертификат).", err.status, err);
        }
        return new SessionBootstrapError("network", "Проблема с сетью или CORS/доступом к API.", err.status, err);
      }

      return new SessionBootstrapError("api", `API вернуло HTTP ${err.status}.`, err.status, err);
    }

    return new SessionBootstrapError("unknown", "Неизвестная ошибка запуска сессии.", undefined, err);
  }

  private looksLikeTlsError(err: HttpErrorResponse): boolean {
    const message = `${err.message ?? ""} ${this.stringifyError(err.error)}`.toLowerCase();
    return ["ssl", "tls", "certificate", "cert", "handshake"].some(mark => message.includes(mark));
  }

  private stringifyError(err: unknown): string {
    if (!err) return "";
    if (typeof err === "string") return err;
    try {
      return JSON.stringify(err);
    } catch {
      return String(err);
    }
  }

  private shouldRetryAnonymousStart(err: SessionBootstrapError): boolean {
    if (err.kind === "timeout" || err.kind === "network" || err.kind === "tls") return true;
    if (err.kind !== "api") return false;
    const status = err.status ?? 0;
    return status >= 500 || status === 429;
  }

  private getAnonymousRetryDelayMs(attempt: number): number {
    const index = Math.max(0, Math.min(attempt - 1, ANON_START_RETRY_DELAYS_MS.length - 1));
    return ANON_START_RETRY_DELAYS_MS[index];
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  private logAnonymousStartError(err: SessionBootstrapError, attempt: number): void {
    const platform = Capacitor.getPlatform();
    const origin = typeof window !== "undefined" ? window.location.origin : "unknown";

    console.warn("[auth] anonymous/start failed", {
      attempt,
      kind: err.kind,
      status: err.status ?? null,
      platform,
      origin,
      apiBase: this.apiBase
    });
  }

  requestStartCode(): Observable<RequestCodeResponse> {
    return this.http.post<RequestCodeResponse>(`${this.apiBase}/api/auth/link/telegram/request-code`, {});
  }

  getStatus(code: string): Observable<StatusResponse> {
    const params = new HttpParams().set("code", code);
    return this.http.get<StatusResponse>(`${this.apiBase}/api/auth/status`, { params });
  }

  exchangeStartCode(code: string, device = "Android-Ionic"): Observable<ExchangeStartCodeResponse | ExchangePending> {
    return this.http.post<ExchangeStartCodeResponse | ExchangePending>(`${this.apiBase}/api/auth/exchange-startcode`, {
      code,
      device,
      installId: this.installId
    });
  }

  openBotWithCode(code: string) {
    const bot = this.normalizeBotUsername(this.botUsername);
    const urlApp = `tg://resolve?domain=${encodeURIComponent(bot)}&start=${encodeURIComponent(code)}`;
    const urlWeb = `https://t.me/${encodeURIComponent(bot)}?start=${encodeURIComponent(code)}`;
    window.location.href = urlApp;
    setTimeout(() => window.open(urlWeb, "_blank"), 400);
  }

  async startLoginFlow(options?: {
    pollIntervalMs?: number;
    pollTimeoutMs?: number;
    deviceLabel?: string;
    onCode?: (code: string, expiresAtUtc: string) => void;
  }): Promise<{ code: string; expiresAtUtc: string; openBot: () => void; waitForJwt: () => Promise<ExchangeStartCodeResponse>; }> {
    const pollEvery = options?.pollIntervalMs ?? 2000;
    const pollTimeout = options?.pollTimeoutMs ?? 120000;
    const device = options?.deviceLabel ?? "Android-Ionic";

    const req = await firstValueFrom(this.requestStartCode());
    options?.onCode?.(req.code, req.expiresAtUtc);

    const openBot = () => this.openBotWithCode(req.code);

    const waitForJwt = async () => {
      const jwt = await firstValueFrom(
        timer(0, pollEvery).pipe(
          // Keep one in-flight request; do not cancel it on next tick.
          exhaustMap(() => this.exchangeStartCode(req.code, device).pipe(
            catchError(err => this.mapExchangeError(err))
          )),
          map(res => ("status" in (res as any)) ? null : (res as ExchangeStartCodeResponse)),
          filter((res): res is ExchangeStartCodeResponse => res !== null),
          take(1),
          timeout({ first: pollTimeout })
        )
      );
      return jwt;
    };

    return { code: req.code, expiresAtUtc: req.expiresAtUtc, openBot, waitForJwt };
  }

  private mapExchangeError(err: unknown): Observable<ExchangeStartCodeResponse | ExchangePending> {
    if (err instanceof HttpErrorResponse) {
      const status = err.status;
      // For auth/validation errors we must stop polling and show explicit backend error.
      if (status >= 400 && status < 500 && status !== 429) {
        return throwError(() => err);
      }
    }

    // Network hiccups / 5xx / temporary overload keep polling.
    return of<ExchangeStartCodeResponse | ExchangePending>({ status: "pending" });
  }
}
