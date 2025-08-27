import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { StatsService, DayStats } from '../../services/stats.service';

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
  data: { date: Date; totals: { calories: number; proteins: number; fats: number; carbs: number } }[] = [];
  maxCalories = 0;

  constructor(private stats: StatsService) {}

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
    this.stats.getDaily(start, end).subscribe(res => {
      this.data = res.map(d => ({ date: new Date(d.date), totals: d.totals }));
      this.maxCalories = Math.max(0, ...this.data.map(d => d.totals.calories));
    });
  }
}
