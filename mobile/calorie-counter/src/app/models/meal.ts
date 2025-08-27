export interface Meal {
  id: string;
  timestamp: number;
  photoUri?: string;
  calories: number;
  proteins: number;
  fats: number;
  carbs: number;
  note?: string;
}

