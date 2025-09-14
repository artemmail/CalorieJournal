import { Injectable, NgZone } from "@angular/core";
import * as signalR from "@microsoft/signalr";
import { Subject, Observable } from "rxjs";
import { MealListItem } from "./foodbot-api.types";
import { FoodBotAuthLinkService } from "./foodbot-auth-link.service";

@Injectable({ providedIn: "root" })
export class HistoryUpdatesService {
  private hub?: signalR.HubConnection;
  private updatesSubj = new Subject<MealListItem>();

  constructor(private auth: FoodBotAuthLinkService, private zone: NgZone) {}

  private ensureStarted() {
    const token = this.auth.token;
    if (!token) return;
    if (this.hub) {
      if (this.hub.state === signalR.HubConnectionState.Disconnected) {
        this.hub.start().catch(err => console.error(err));
      }
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
    this.hub.start().catch(err => console.error(err));
  }

  updates(): Observable<MealListItem> {
    this.ensureStarted();
    return this.updatesSubj.asObservable();
  }
}
