import { CanActivateFn, Router } from "@angular/router";
import { inject } from "@angular/core";
import { FoodBotAuthLinkService } from "../services/foodbot-auth-link.service";

export const authGuard: CanActivateFn = () => {
  const auth = inject(FoodBotAuthLinkService);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  router.navigateByUrl("/auth");
  return false;
};
