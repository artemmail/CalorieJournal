import { Injectable, NgZone } from "@angular/core";
import * as signalR from "@microsoft/signalr";
import { Subject, Observable } from "rxjs";
import { MealListItem } from "./foodbot-api.types";
import { FoodBotAuthLinkService } from "./foodbot-auth-link.service";

@Injectable({ providedIn: "root" })
export class HistoryUpdatesService {
  private hub?: signalR.HubConnection;
  private updatesSubj = new Subject<MealListItem>();
  private restartTimer?: ReturnType<typeof setTimeout>;
  private starting = false;

  constructor(private auth: FoodBotAuthLinkService, private zone: NgZone) {
    this.auth.tokenChanges().subscribe(token => {
      if (this.hub) {
        this.clearRestartTimer();
        this.hub.stop().finally(() => {
          if (token) {
            this.tryStart();
          }
        });
      } else if (token) {
        this.ensureStarted();
      }
    });
  }

  private ensureStarted() {
    if (this.hub) {
      this.tryStart();
      return;
    }

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(`${this.auth.apiBaseUrl}/hubs/meals`, {
        accessTokenFactory: () => this.auth.token ?? ""
      })
      .withAutomaticReconnect()
      .build();

    this.hub.on("MealUpdated", (item: MealListItem) => {
      this.zone.run(() => this.updatesSubj.next(item));
    });

    this.hub.onclose(() => this.scheduleRestart());
    this.tryStart();
  }

  private async tryStart() {
    if (!this.hub || this.starting) return;

    if (this.hub.state === signalR.HubConnectionState.Connected ||
        this.hub.state === signalR.HubConnectionState.Connecting ||
        this.hub.state === signalR.HubConnectionState.Reconnecting) {
      return;
    }

    this.starting = true;
    this.clearRestartTimer();
    try {
      if (!this.auth.token) {
        await this.auth.ensureSession();
      }
      if (!this.auth.token) {
        this.scheduleRestart();
        return;
      }
      await this.hub.start();
    } catch (err) {
      console.error(err);
      this.scheduleRestart();
    } finally {
      this.starting = false;
    }
  }

  private scheduleRestart(delayMs = 3000) {
    if (this.restartTimer) return;
    this.restartTimer = setTimeout(() => {
      this.restartTimer = undefined;
      this.tryStart();
    }, delayMs);
  }

  private clearRestartTimer() {
    if (!this.restartTimer) return;
    clearTimeout(this.restartTimer);
    this.restartTimer = undefined;
  }

  updates(): Observable<MealListItem> {
    this.ensureStarted();
    return this.updatesSubj.asObservable();
  }
}
