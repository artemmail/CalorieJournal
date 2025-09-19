import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { TextFieldModule } from "@angular/cdk/text-field";
import { FormControl, ReactiveFormsModule } from "@angular/forms";

@Component({
  selector: "app-voice-input-panel",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    TextFieldModule
  ],
  templateUrl: "./voice-input-panel.component.html",
  styleUrls: ["./voice-input-panel.component.scss"],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VoiceInputPanelComponent {
  @Input() label = "";
  @Input() placeholder = "";
  @Input() minRows = 4;
  @Input() maxRows = 12;
  @Input() control!: FormControl<string | null>;
  @Input() isRecording = false;
  @Input() disabled = false;
  @Input() idleHint = "Удерживайте для записи";
  @Input() recordingHint = "Запись… отпустите, чтобы остановить";
  @Input() showHints = true;

  @Output() cleared = new EventEmitter<void>();
  @Output() startRecord = new EventEmitter<Event>();
  @Output() stopRecord = new EventEmitter<void>();

  onPress(event: Event) {
    event.preventDefault();
    this.startRecord.emit(event);
  }

  onRelease() {
    this.stopRecord.emit();
  }

  onClear(event: MouseEvent) {
    event.preventDefault();
    this.cleared.emit();
  }

  get hasValue(): boolean {
    const value = this.control.value;
    return typeof value === "string" ? value.length > 0 : !!value;
  }
}
