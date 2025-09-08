import "zone.js";
import { bootstrapApplication } from "@angular/platform-browser";
import { provideRouter } from "@angular/router";
import { provideAnimations } from "@angular/platform-browser/animations";
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from "@angular/common/http";
import { AppComponent } from "./app/app.component";
import { routes } from "./app/routes";
import { AuthInterceptor } from "./app/services/auth.interceptor";
import { Capacitor } from "@capacitor/core";
import { StatusBar } from "@capacitor/status-bar";

// Configure Android status and navigation bars
(async () => {
  try {
    if (Capacitor.getPlatform() === "android") {
      await StatusBar.setOverlaysWebView({ overlay: false });
      await StatusBar.setBackgroundColor({ color: "#000000" });
      const { NavigationBar } = await import("@capgo/capacitor-navigation-bar");
      await NavigationBar.setColor({ color: "#000000" });
      await NavigationBar.setButtonStyle({ style: "LIGHT" });
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

