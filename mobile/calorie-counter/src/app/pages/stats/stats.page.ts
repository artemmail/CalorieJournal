import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MealService } from '../../services/meal.service';

interface DayTotals {
  date: Date;
  totals: { calories: number; proteins: number; fats: number; carbs: number };
}

@Component({
  selector: 'app-stats',
  standalone: true,
  imports: [CommonModule, FormsModule, MatCardModule, MatButtonToggleModule, MatFormFieldModule, MatSelectModule],
  templateUrl: './stats.page.html',
  styleUrls: ['./stats.page.scss']
})
export class StatsPage implements OnInit {
  view: 'chart' | 'table' = 'chart';
  selectedPeriod = 'week';
  customStart = '';
  customEnd = '';
  data: DayTotals[] = [];
  maxCalories = 0;

  constructor(private meals: MealService) {}

  ngOnInit() { this.updatePeriod(); }

  updatePeriod() {
    const end = new Date();
    end.setHours(0,0,0,0);
    let start = new Date(end);
    if (this.selectedPeriod === 'week') start.setDate(end.getDate() - 6);
    else if (this.selectedPeriod === 'month') start.setDate(end.getDate() - 29);
    else if (this.selectedPeriod === 'quarter') start.setDate(end.getDate() - 89);
    else {
      if (this.customStart) start = new Date(this.customStart);
      if (this.customEnd) end.setTime(new Date(this.customEnd).getTime());
    }
    this.data = this.meals.dailyTotals(start, end);
    this.maxCalories = Math.max(0, ...this.data.map(d => d.totals.calories));
  }
}
