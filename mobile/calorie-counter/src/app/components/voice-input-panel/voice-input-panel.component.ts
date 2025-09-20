import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, Output } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { TextFieldModule } from "@angular/cdk/text-field";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { MatSnackBar } from "@angular/material/snack-bar";
import { firstValueFrom } from "rxjs";

import { FoodbotApiService } from "../../services/foodbot-api.service";
import { VoiceService } from "../../services/voice.service";

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
    TextFieldModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./voice-input-panel.component.html",
  styleUrls: ["./voice-input-panel.component.scss"],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VoiceInputPanelComponent implements OnDestroy {
  @Input() label = "";
  @Input() placeholder = "";
  @Input() minRows = 4;
  @Input() maxRows = 12;
  @Input() control!: FormControl<string | null>;
  @Input() disabled = false;
  @Input() idleHint = "Удерживайте для записи";
  @Input() recordingHint = "Запись… отпустите, чтобы остановить";
  @Input() showHints = true;

  @Output() transcribingChange = new EventEmitter<boolean>();

  isRecording = false;
  protected transcribing = false;

  private recorder?: MediaRecorder;
  private chunks: Blob[] = [];

  constructor(
    private api: FoodbotApiService,
    private snack: MatSnackBar,
    private voice: VoiceService
  ) {}

  onPress(event: Event) {
    event.preventDefault();
    void this.startRecording();
  }

  onRelease() {
    void this.stopRecording();
  }

  get isMicDisabled(): boolean {
    return this.disabled || this.transcribing;
  }

  ngOnDestroy() {
    if (this.recorder && this.recorder.state !== "inactive") {
      try {
        this.recorder.stop();
      } catch {
        // ignore
      }
    }
    this.recorder = undefined;
    this.chunks = [];
    this.setTranscribing(false);
    this.isRecording = false;
  }

  private async startRecording() {
    if (this.isMicDisabled || this.recorder) return;
    try {
      const granted = await this.voice.ensurePermission();
      if (!granted) {
        this.snack.open("Разрешите доступ к микрофону, чтобы записывать голос", "OK", {
          duration: 2000
        });
        return;
      }
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.chunks = [];
      const recorder = new MediaRecorder(stream);
      recorder.addEventListener(
        "dataavailable",
        event => {
          if (event.data.size > 0) this.chunks.push(event.data);
        }
      );
      recorder.addEventListener(
        "stop",
        () => {
          stream.getTracks().forEach(track => track.stop());
          this.isRecording = false;
        },
        { once: true }
      );
      recorder.start();
      this.recorder = recorder;
      this.isRecording = true;
    } catch {
      this.recorder = undefined;
      this.isRecording = false;
      this.snack.open("Не удалось начать запись с микрофона", "OK", { duration: 1500 });
    }
  }

  private async stopRecording() {
    const recorder = this.recorder;
    if (!recorder) return;
    this.recorder = undefined;
    const stopped = new Promise<void>(resolve =>
      recorder.addEventListener("stop", () => resolve(), { once: true })
    );
    recorder.stop();
    await stopped;
    const chunks = this.chunks.slice();
    this.chunks = [];
    if (!chunks.length) return;

    this.setTranscribing(true);
    try {
      const blob = new Blob(chunks, { type: "audio/webm" });
      const file = new File([blob], "voice.webm", { type: blob.type });
      const result = await firstValueFrom(this.api.transcribeVoice(file));
      if (result.text) this.control?.setValue(result.text);
    } catch {
      this.snack.open("Не удалось распознать речь", "OK", { duration: 1500 });
    } finally {
      this.setTranscribing(false);
    }
  }

  private setTranscribing(value: boolean) {
    if (this.transcribing === value) return;
    this.transcribing = value;
    this.transcribingChange.emit(value);
  }
}
