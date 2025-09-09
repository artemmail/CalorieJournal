import { Component, ViewChild, OnInit, AfterViewInit, signal } from "@angular/core";
import { RouterOutlet, Router, NavigationEnd, RouterLink, RouterLinkActive } from "@angular/router";
import { filter } from 'rxjs/operators';
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatSidenavModule, MatSidenav } from "@angular/material/sidenav";
import { SideMenuComponent } from "./components/side-menu/side-menu.component";
import { NavigationBar } from '@capgo/capacitor-navigation-bar';
import { Capacitor } from '@capacitor/core';
import { SafeArea } from 'capacitor-plugin-safe-area';
import { StatusBar } from '@capacitor/status-bar';

@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatIconModule, MatButtonModule, MatSidenavModule,
    SideMenuComponent
  ],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnInit, AfterViewInit {
  title = 'calorie-counter';
  @ViewChild('drawer') drawer!: MatSidenav;

  readonly TOPBAR_HEIGHT = 64;
  readonly BOTTOMBAR_HEIGHT = 56;

  safeTop = signal(0);
  safeBottom = signal(0);

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => {
        if (this.drawer?.opened) this.drawer.close();
      });
  }

  async ngOnInit() {
    
    const isWeb = Capacitor.getPlatform() === 'web';
    if (!isWeb) {
      try {
        await StatusBar.setOverlaysWebView({ overlay: true });
        await StatusBar.setBackgroundColor({ color: '#00000000' });
        await NavigationBar.setNavigationBarColor({ color: '#00000000', darkButtons: true });
      } catch {  }
    }
  }

  async ngAfterViewInit() {
    try {
      const { insets } = await SafeArea.getSafeAreaInsets();
      const { statusBarHeight } = await SafeArea.getStatusBarHeight();

      let top = Math.max(statusBarHeight ?? 0, 0);
      let bottom = Math.max(insets?.bottom ?? 0, 0);

      /*
      
      // 🔴 Фолбэк для веба: чтобы было видно красные вкладыши при нулевых инсетах
      const isWeb = Capacitor.getPlatform() === 'web';
      if (isWeb && top === 0 && bottom === 0) {
        top = 24;     // можно уменьшить/увеличить под себя
        bottom = 24;
      }
*/
      this.safeTop.set(top);
      this.safeBottom.set(bottom);
    } catch {
      // Ещё один фолбэк (веб/разработка)
      this.safeTop.set(24);
      this.safeBottom.set(24);
    }
  }
}
