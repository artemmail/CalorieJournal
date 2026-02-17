import { CanActivateFn, Router } from "@angular/router";
import { inject } from "@angular/core";
import { FoodBotAuthLinkService } from "../services/foodbot-auth-link.service";

export const authGuard: CanActivateFn = async () => {
  const auth = inject(FoodBotAuthLinkService);
  const router = inject(Router);

  try {
    await auth.ensureSession();
    return auth.isAuthenticated();
  } catch {
    router.navigateByUrl("/auth");
    return false;
  }
};
