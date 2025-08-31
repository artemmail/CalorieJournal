import { Component, ViewChild } from "@angular/core";
import { RouterOutlet, Router, NavigationEnd } from "@angular/router";
import { filter } from 'rxjs/operators';
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatSidenavModule, MatSidenav } from "@angular/material/sidenav";
import { SideMenuComponent } from "./components/side-menu/side-menu.component";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, MatToolbarModule, MatIconModule, MatButtonModule, MatSidenavModule, SideMenuComponent],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent {
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
}
