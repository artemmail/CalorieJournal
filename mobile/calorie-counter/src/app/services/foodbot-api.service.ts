import { Injectable } from "@angular/core";
import { HttpClient, HttpParams, HttpEvent } from "@angular/common/http";
import { map, Observable } from "rxjs";
import { FoodBotAuthLinkService } from "./foodbot-auth-link.service";
import {
  ClarifyResult,
  MealDetails,
  MealsListResponse,
  PersonalCard
} from "./foodbot-api.types";

@Injectable({ providedIn: "root" })
export class FoodbotApiService {
  constructor(private http: HttpClient, private auth: FoodBotAuthLinkService) {}
  private get baseUrl(): string { return this.auth.apiBaseUrl; }

  // История
  getMeals(limit = 30, offset = 0): Observable<MealsListResponse> {
    const params = new HttpParams().set("limit", limit).set("offset", offset);
    return this.http.get<MealsListResponse>(`${this.baseUrl}/api/meals`, { params });
  }
  getMeal(id: number): Observable<MealDetails> {
    return this.http.get<MealDetails>(`${this.baseUrl}/api/meals/${id}`);
  }
  deleteMeal(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/meals/${id}`);
  }
  getMealImageBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/api/meals/${id}/image`, { responseType: "blob" as const });
  }
  getMealImageObjectUrl(id: number): Observable<string> {
    return this.getMealImageBlob(id).pipe(map(blob => URL.createObjectURL(blob)));
  }

  // Загрузка фото
  uploadPhoto(file: File): Observable<HttpEvent<{ queued: boolean }>> {
    const form = new FormData();
    form.append("image", file, file.name);
    return this.http.post<{ queued: boolean }>(
      `${this.baseUrl}/api/meals/upload`,
      form,
      { reportProgress: true, observe: "events" }
    );
  }

  // Уточнения
  clarifyText(mealId: number, note?: string, time?: string): Observable<ClarifyResult> {
    const body: any = {};
    if (note) body.note = note;
    if (time) body.time = time;
    return this.http.post<ClarifyResult>(`${this.baseUrl}/api/meals/${mealId}/clarify-text`, body);
  }
  clarifyVoice(mealId: number, file: File, language = "ru"): Observable<ClarifyResult> {
    const form = new FormData();
    form.append("audio", file, file.name);
    form.append("language", language);
    return this.http.post<ClarifyResult>(`${this.baseUrl}/api/meals/${mealId}/clarify-voice`, form);
  }

  transcribeVoice(file: File, language = "ru"): Observable<{ text: string }> {
    const form = new FormData();
    form.append("audio", file, file.name);
    form.append("language", language);
    return this.http.post<{ text: string }>(`${this.baseUrl}/api/stt`, form);
  }

  // Отчёт Excel
  getReport(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/api/meals/report`, { responseType: "blob" as const });
  }

  // Личная карточка
  getPersonalCard(): Observable<PersonalCard | null> {
    return this.http.get<PersonalCard | null>(`${this.baseUrl}/api/profile`);
  }
  savePersonalCard(card: PersonalCard): Observable<PersonalCard> {
    return this.http.post<PersonalCard>(`${this.baseUrl}/api/profile`, card);
  }
}


