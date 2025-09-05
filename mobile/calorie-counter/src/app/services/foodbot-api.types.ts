export interface Step1Snapshot {
  dish: string;
  ingredients: string[];
  shares_percent: number[];
  weight_g: number;
  confidence: number; // 0..1
}

export interface NutritionResult {
  dish: string;
  ingredients: string[];
  proteins_g: number;
  fats_g: number;
  carbs_g: number;
  calories_kcal: number;
  weight_g: number;
  confidence: number; // 0..1
}

export interface ProductInfo {
  name: string;
  grams: number;
  proteins_g: number;
  fats_g: number;
  carbs_g: number;
  calories_kcal: number;
  percent: number;
}

export interface MealListItem {
  id: number;
  createdAtUtc: string;
  dishName: string | null;
  weightG: number | null;
  caloriesKcal: number | null;
  proteinsG: number | null;
  fatsG: number | null;
  carbsG: number | null;
  ingredients: string[];
  products: ProductInfo[];
  hasImage: boolean;
  updateQueued: boolean;
  expanded?: boolean;
}

export interface MealsListResponse {
  total: number;
  offset: number;
  limit: number;
  items: MealListItem[];
}

export interface MealDetails {
  id: number;
  createdAtUtc: string;
  dishName: string | null;
  weightG: number | null;
  caloriesKcal: number | null;
  proteinsG: number | null;
  fatsG: number | null;
  carbsG: number | null;
  confidence: number | null;
  ingredients: string[];
  products: ProductInfo[];
  clarifyNote: string | null;
  step1: Step1Snapshot | null;
  reasoningPrompt: string | null;
  hasImage: boolean;
}

export interface ClarifyResult {
  id: number;
  createdAtUtc: string;
  result: NutritionResult;
  products: ProductInfo[];
  step1: Step1Snapshot;
  reasoningPrompt: string;
  calcPlanJson: string;
}

export interface ExchangeResponse {
  accessToken: string;
  tokenType: string;        // "Bearer"
  expiresInSeconds: number;
  chatId: number;
}

export interface PersonalCard {
  email: string | null;
  name: string | null;
  birthYear: number | null;
  dietGoals: string | null;
  medicalRestrictions: string | null;
}
