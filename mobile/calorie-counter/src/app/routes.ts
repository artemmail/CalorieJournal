import { Routes } from "@angular/router";
import { HistoryPage } from "./pages/history/history.page";
import { AddMealPage } from "./pages/add-meal/add-meal.page";
import { AnalysisPage } from "./pages/analysis/analysis.page";
import { AnalysisReportPage } from "./pages/analysis-report/analysis-report.page";
import { AuthPage } from "./pages/auth/auth.page";
import { StatsPage } from "./pages/stats/stats.page";
import { authGuard } from "./guards/auth.guard";
import { ProfilePage } from "./pages/profile/profile.page";

export const routes: Routes = [
  { path: "auth", component: AuthPage },
  { path: "", redirectTo: "history", pathMatch: "full" },
  { path: "history", component: HistoryPage, canActivate: [authGuard] },
  { path: "add", component: AddMealPage, canActivate: [authGuard] },
  { path: "analysis/:id", component: AnalysisReportPage, canActivate: [authGuard] },
  { path: "analysis", component: AnalysisPage, canActivate: [authGuard] },
  { path: "stats", component: StatsPage, canActivate: [authGuard] },
  { path: "profile", component: ProfilePage, canActivate: [authGuard] },
  { path: "**", redirectTo: "history" }
];
