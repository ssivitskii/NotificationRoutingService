import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DeadLetterResponse,
  DeliveryResponse,
  Importance,
  MessageResponse,
  ProblemDetails,
  PublishAcceptedResponse,
  TopicResponse,
  UserResponse,
} from './notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);

  createUser(name: string, alertKeywords: string[]): Observable<UserResponse> {
    return this.http.post<UserResponse>('/api/users', { name, alertKeywords });
  }

  createTopic(name: string): Observable<TopicResponse> {
    return this.http.post<TopicResponse>('/api/topics', { name });
  }

  subscribe(topicId: string, userId: string, minimumImportance: Importance): Observable<void> {
    return this.http.post<void>(`/api/topics/${topicId}/subscribers`, {
      userId,
      minimumImportance,
    });
  }

  publish(
    topicId: string,
    title: string,
    body: string,
    importance: Importance,
    idempotencyKey: string,
  ): Observable<PublishAcceptedResponse> {
    return this.http.post<PublishAcceptedResponse>(
      `/api/topics/${topicId}/messages`,
      { title, body, importance },
      { headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }) },
    );
  }

  getDelivery(messageId: string): Observable<DeliveryResponse> {
    return this.http.get<DeliveryResponse>(`/api/deliveries/${messageId}`);
  }

  getInbox(userId: string): Observable<MessageResponse[]> {
    return this.http.get<MessageResponse[]>(`/api/users/${userId}/messages`);
  }

  getArchive(): Observable<MessageResponse[]> {
    return this.http.get<MessageResponse[]>('/api/archive');
  }

  getDeadLetters(): Observable<DeadLetterResponse[]> {
    return this.http.get<DeadLetterResponse[]>('/api/deliveries/dead-letter');
  }

  markRead(userId: string, messageId: string): Observable<void> {
    return this.http.put<void>(`/api/users/${userId}/messages/${messageId}/read`, null);
  }

  retry(deliveryId: string): Observable<void> {
    return this.http.post<void>(`/api/deliveries/${deliveryId}/retry`, null);
  }
}

export function problemDetail(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    const validation = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
    if (validation) return validation;
    if (typeof problem?.detail === 'string') return problem.detail;
    if (typeof problem?.title === 'string') return problem.title;
    if (error.status === 0) return 'The notification service is unavailable.';
  }
  return 'The operation could not be completed.';
}
