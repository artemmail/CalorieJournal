import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { AnalysisService, AnalysisPeriod } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatTabsModule, MarkdownPipe],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage implements OnInit, OnDestroy {
  period: AnalysisPeriod = 'day';

  constructor(public a: AnalysisService) {}

  ngOnInit() {
    this.a.refresh(this.period);
  }

  ngOnDestroy() {
    this.a.cancel();
  }

  changePeriod(index: number) {
    const map: AnalysisPeriod[] = ['day', 'week', 'month', 'quarter'];
    this.period = map[index] ?? 'day';
    this.a.refresh(this.period);
  }
}
