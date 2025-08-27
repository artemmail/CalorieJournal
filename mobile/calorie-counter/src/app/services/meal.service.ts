import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Meal } from '../models/meal';
import { StorageService } from './storage.service';

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Injectable({ providedIn: 'root' })
export class MealService {
  private _meals$ = new BehaviorSubject<Meal[]>([]);
  meals$ = this._meals$.asObservable();

  constructor(private storage: StorageService) {
    this.init();
  }

  async init() {
    const meals = await this.storage.loadMeals();
    this._meals$.next(meals);
  }

  private persist() { this.storage.saveMeals(this._meals$.value); }

  getAll(): Meal[] { return this._meals$.value.sort((a, b) => b.timestamp - a.timestamp); }

  add(meal: Omit<Meal, 'id'>) {
    const m: Meal = { id: uuid(), ...meal };
    const next = [m, ...this._meals$.value];
    this._meals$.next(next);
    this.persist();
  }

  update(id: string, patch: Partial<Meal>) {
    const next = this._meals$.value.map(m => (m.id === id ? { ...m, ...patch } : m));
    this._meals$.next(next);
    this.persist();
  }

  remove(id: string) {
    const next = this._meals$.value.filter(m => m.id !== id);
    this._meals$.next(next);
    this.persist();
  }

  sumForDate(date: Date) {
    const start = new Date(date); start.setHours(0, 0, 0, 0);
    const end = new Date(date); end.setHours(23, 59, 59, 999);
    return this.sumRange(start.getTime(), end.getTime());
  }

  sumDaysBack(days: number) {
    const end = Date.now();
    const start = end - days * 24 * 60 * 60 * 1000;
    return this.sumRange(start, end);
  }

  dailyTotals(start: Date, end: Date) {
    const results: { date: Date; totals: { calories: number; proteins: number; fats: number; carbs: number } }[] = [];
    const dayMs = 24 * 60 * 60 * 1000;
    const s = new Date(start); s.setHours(0, 0, 0, 0);
    const e = new Date(end); e.setHours(0, 0, 0, 0);
    for (let t = s.getTime(); t <= e.getTime(); t += dayMs) {
      const { totals } = this.sumRange(t, t + dayMs - 1);
      results.push({ date: new Date(t), totals });
    }
    return results;
  }

  private sumRange(startMs: number, endMs: number) {
    const meals = this._meals$.value.filter(m => m.timestamp >= startMs && m.timestamp <= endMs);
    const totals = meals.reduce((acc, m) => {
      acc.calories += m.calories || 0;
      acc.proteins += m.proteins || 0;
      acc.fats += m.fats || 0;
      acc.carbs += m.carbs || 0;
      return acc;
    }, { calories: 0, proteins: 0, fats: 0, carbs: 0 });
    return { totals, count: meals.length };
  }
}

