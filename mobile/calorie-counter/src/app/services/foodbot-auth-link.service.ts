/**
 * FoodBotAuthLinkService — аутентификация через старт-код от сервера.
 */
import { Injectable } from "@angular/core";
import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Observable, interval, firstValueFrom, of, Subject } from "rxjs";
import { catchError, filter, map, switchMap, take, timeout } from "rxjs/operators";

export type ExternalProvider = 'telegram' | 'vk';
export interface RequestCodeResponse { code: string; expiresAtUtc: string; }
export interface StatusResponse { linked: boolean; expiresAtUtc: string; secondsLeft: number; }
export interface ExchangeStartCodeResponse {
  accessToken: string; tokenType: 'Bearer'; expiresInSeconds: number; userId: number;
}
export interface ExchangePending { status: 'pending'; }

const JWT_KEY = 'foodbot.jwt';
const JWT_EXP  = 'foodbot.jwt.exp';
declare const environment: any;

@Injectable({ providedIn: 'root' })
export class FoodBotAuthLinkService {
  constructor(private http: HttpClient) {}

  private tokenChangedSubj = new Subject<string | null>();
  tokenChanges(): Observable<string | null> { return this.tokenChangedSubj.asObservable(); }
  readonly supportedProviders: ExternalProvider[] = ['telegram', 'vk'];

  private get apiBase(): string {
    return (window as any).environment?.apiBaseUrl ?? environment?.apiBaseUrl ?? '';
  }
  get apiBaseUrl(): string { return this.apiBase; }

  private get botUsername(): string {
    return (window as any).environment?.telegramBot ?? environment?.telegramBot ?? 'your_bot_username';
  }

  getDefaultProvider(): ExternalProvider {
    const envProvider = ((window as any).environment?.defaultAuthProvider ?? environment?.defaultAuthProvider) as ExternalProvider | undefined;
    if (envProvider && this.supportedProviders.includes(envProvider)) return envProvider;
    return 'telegram';
  }

  get token(): string | null {
    const t = localStorage.getItem(JWT_KEY);
    const exp = Number(localStorage.getItem(JWT_EXP) ?? 0);
    if (!t) return null;
    if (exp && Date.now() > exp) { this.logout(); return null; }
    return t;
  }
  isAuthenticated(): boolean { return !!this.token; }

  setToken(resp: ExchangeStartCodeResponse) {
    const expMs = Date.now() + resp.expiresInSeconds * 1000;
    localStorage.setItem(JWT_KEY, resp.accessToken);
    localStorage.setItem(JWT_EXP, String(expMs));
    this.tokenChangedSubj.next(resp.accessToken);
  }
  logout() {
    localStorage.removeItem(JWT_KEY);
    localStorage.removeItem(JWT_EXP);
    this.tokenChangedSubj.next(null);
  }

  authHeaders(): HttpHeaders {
    const t = this.token; return new HttpHeaders(t ? { Authorization: `Bearer ${t}` } : {});
  }

  requestStartCode(): Observable<RequestCodeResponse> {
    return this.http.post<RequestCodeResponse>(`${this.apiBase}/api/auth/request-code`, {});
  }

  getStatus(code: string): Observable<StatusResponse> {
    const params = new HttpParams().set('code', code);
    return this.http.get<StatusResponse>(`${this.apiBase}/api/auth/status`, { params });
  }

  exchangeStartCode(code: string, device = 'Android-Ionic'): Observable<ExchangeStartCodeResponse | ExchangePending> {
    return this.http.post<ExchangeStartCodeResponse | ExchangePending>(`${this.apiBase}/api/auth/exchange-startcode`, { code, device });
  }

  externalLogin(provider: ExternalProvider, externalId: string, username?: string, device = 'Android-Ionic'): Promise<ExchangeStartCodeResponse> {
    const body = { provider, externalId, username, device };
    return firstValueFrom(this.http.post<ExchangeStartCodeResponse>(`${this.apiBase}/api/auth/external-login`, body));
  }

  openVk(returnUrl?: string, newTab = false) {
    const backUrl = returnUrl ?? window.location.href;
    const url = `${this.apiBase}/api/auth/vk/start?returnUrl=${encodeURIComponent(backUrl)}`;
    if (newTab) window.open(url, "_blank");
    else window.location.href = url;
  }

  openBotWithCode(code: string) {
    const bot = this.botUsername.replace(/^@/, '');
    const urlApp = `tg://resolve?domain=${encodeURIComponent(bot)}&start=${encodeURIComponent(code)}`;
    const urlWeb = `https://t.me/${encodeURIComponent(bot)}?start=${encodeURIComponent(code)}`;
    window.location.href = urlApp;
    setTimeout(() => window.open(urlWeb, '_blank'), 400);
  }

  async startLoginFlow(options?: {
    pollIntervalMs?: number;
    pollTimeoutMs?: number;
    deviceLabel?: string;
    onCode?: (code: string, expiresAtUtc: string) => void;
  }): Promise<{ code: string; expiresAtUtc: string; openBot: () => void; waitForJwt: () => Promise<ExchangeStartCodeResponse>; }> {
    const pollEvery = options?.pollIntervalMs ?? 2000;
    const pollTimeout = options?.pollTimeoutMs ?? 120000;
    const device = options?.deviceLabel ?? 'Android-Ionic';

    const req = await firstValueFrom(this.requestStartCode());
    options?.onCode?.(req.code, req.expiresAtUtc);

    const openBot = () => this.openBotWithCode(req.code);

    const waitForJwt = async () => {
      return await firstValueFrom(
        interval(pollEvery).pipe(
          switchMap(() => this.exchangeStartCode(req.code, device).pipe(
            catchError(() => of<ExchangeStartCodeResponse | ExchangePending>({ status: 'pending' }))
          )),
          map(res => ('status' in (res as any)) ? null : (res as ExchangeStartCodeResponse)),
          filter((res): res is ExchangeStartCodeResponse => res !== null),
          timeout({ first: pollTimeout }),
          take(1)
        )
      );
    };

    return { code: req.code, expiresAtUtc: req.expiresAtUtc, openBot, waitForJwt };
  }
}
