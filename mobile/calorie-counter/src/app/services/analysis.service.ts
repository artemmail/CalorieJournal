import { Injectable } from '@angular/core';
import { MealService } from './meal.service';

export interface Goals { calories: number; proteins: number; fats: number; carbs: number; }

const DEFAULT_GOALS: Goals = { calories: 2000, proteins: 100, fats: 70, carbs: 250 };

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  goals: Goals = { ...DEFAULT_GOALS };

  constructor(private meals: MealService) {}

  todayRecommendation() {
    const today = this.meals.sumForDate(new Date());
    const g = this.goals;
    return {
      consumed: today.totals,
      remaining: {
        calories: Math.max(0, g.calories - today.totals.calories),
        proteins: Math.max(0, g.proteins - today.totals.proteins),
        fats: Math.max(0, g.fats - today.totals.fats),
        carbs: Math.max(0, g.carbs - today.totals.carbs)
      },
      tip: this.tipForRemainder(g, today.totals)
    };
  }

  periodSummary(days: number) {
    const { totals, count } = this.meals.sumDaysBack(days);
    const avgDaily = {
      calories: totals.calories / days,
      proteins: totals.proteins / days,
      fats: totals.fats / days,
      carbs: totals.carbs / days
    };
    return {
      totals, days, entries: count, avgDaily,
      deviationFromGoals: {
        calories: avgDaily.calories - this.goals.calories,
        proteins: avgDaily.proteins - this.goals.proteins,
        fats: avgDaily.fats - this.goals.fats,
        carbs: avgDaily.carbs - this.goals.carbs
      },
      recommendation: this.recommendForPeriod(avgDaily)
    };
  }

  private tipForRemainder(goals: Goals, tot: any) {
    const remainCal = goals.calories - tot.calories;
    if (remainCal > 400) return 'До конца дня: сделай плотный приём пищи 400–700 ккал (белок 25–40 г).';
    if (remainCal > 150) return 'Подойдёт перекус 150–300 ккал, с акцентом на белок.';
    if (remainCal > 0)   return 'Финишируй лёгким перекусом до 150 ккал.';
    return 'Лимит на сегодня достигнут — держи воду/чай, без лишних калорий.';
  }

  private recommendForPeriod(avg: any) {
    const over = avg.calories - this.goals.calories;
    if (over > 150)   return 'В среднем переедание. Уменьши 200–300 ккал/день; добавь овощи и белок.';
    if (over < -150)  return 'В среднем недобор. Добавь 150–250 ккал/день, держи белок ≥ 100 г.';
    return 'Баланс около цели. Продолжай в том же духе; следи за белком и клетчаткой.';
  }
}
