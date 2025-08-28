import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { AnalysisService } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MarkdownPipe],
  templateUrl: './analysis.page.html',
  styleUrls: ['./analysis.page.scss']
})
export class AnalysisPage implements OnInit, OnDestroy {
  constructor(public a: AnalysisService) {}

  ngOnInit() {
    this.a.refresh();
  }

  ngOnDestroy() {
    this.a.cancel();
  }
}
