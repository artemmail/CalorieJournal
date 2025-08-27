import { Routes } from "@angular/router";
import { HistoryPage } from "./pages/history/history.page";
import { AddMealPage } from "./pages/add-meal/add-meal.page";
import { AnalysisPage } from "./pages/analysis/analysis.page";
import { AuthPage } from "./pages/auth/auth.page";
import { authGuard } from "./guards/auth.guard";

export const routes: Routes = [
  { path: "auth", component: AuthPage },
  { path: "", redirectTo: "history", pathMatch: "full" },
  { path: "history", component: HistoryPage, canActivate: [authGuard] },
  { path: "add", component: AddMealPage, canActivate: [authGuard] },
  { path: "analysis", component: AnalysisPage, canActivate: [authGuard] },
  { path: "**", redirectTo: "history" }
];
