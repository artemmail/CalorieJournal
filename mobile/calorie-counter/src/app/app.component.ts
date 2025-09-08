import { Component, ViewChild, OnInit, AfterViewInit } from "@angular/core";
import { RouterOutlet, Router, NavigationEnd, RouterLink, RouterLinkActive } from "@angular/router";
import { filter } from 'rxjs/operators';
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatSidenavModule, MatSidenav } from "@angular/material/sidenav";
import { SideMenuComponent } from "./components/side-menu/side-menu.component";
import { StatusBar } from "@capacitor/status-bar";
import { SafeArea } from 'capacitor-plugin-safe-area';

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatIconModule, MatButtonModule, MatSidenavModule, SideMenuComponent],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnInit, AfterViewInit {
  title = 'calorie-counter';
  @ViewChild('drawer') drawer!: MatSidenav;

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => {
        if (this.drawer?.opened) {
          this.drawer.close();
        }
      });
  }

  async ngOnInit() {
    await StatusBar.setOverlaysWebView({ overlay: false });
  }

  async ngAfterViewInit() {
    const { insets } = await SafeArea.getSafeAreaInsets();
    const { statusBarHeight } = await SafeArea.getStatusBarHeight();
    document.documentElement.style.setProperty('--ion-safe-area-top', `${statusBarHeight}px`);
    document.documentElement.style.setProperty('--ion-safe-area-bottom', `${insets.bottom}px`);
  }
}
