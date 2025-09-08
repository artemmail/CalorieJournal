import "zone.js";
import { bootstrapApplication } from "@angular/platform-browser";
import { provideRouter } from "@angular/router";
import { provideAnimations } from "@angular/platform-browser/animations";
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from "@angular/common/http";
import { AppComponent } from "./app/app.component";
import { routes } from "./app/routes";
import { AuthInterceptor } from "./app/services/auth.interceptor";
import { Capacitor } from "@capacitor/core";
import { StatusBar, Style as StatusBarStyle } from "@capacitor/status-bar";

// Configure Android status bar
(async () => {
  try {
    if (Capacitor.getPlatform() === "android") {
      await StatusBar.setOverlaysWebView({ overlay: true });
      await StatusBar.setBackgroundColor({ color: "#ffffff" });
      await StatusBar.setStyle({ style: StatusBarStyle.Dark });
    }
  } catch {}
})();

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
  ]
}).catch(err => console.error(err));

