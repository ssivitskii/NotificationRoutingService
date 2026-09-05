import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationApiService, problemDetail } from './notification-api.service';

describe('NotificationApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('sends setup and publish requests with the API-owned routing inputs', () => {
    const service = TestBed.inject(NotificationApiService);
    const http = TestBed.inject(HttpTestingController);

    service.createUser('Maya', ['security']).subscribe();
    const user = http.expectOne('/api/users');
    expect(user.request.method).toBe('POST');
    expect(user.request.body).toEqual({ name: 'Maya', alertKeywords: ['security'] });
    expect(user.request.body.webhookUrl).toBeUndefined();
    user.flush({ id: 'user-1', name: 'Maya', alertKeywords: ['security'], webhookUrl: null });

    service.createTopic('Operations').subscribe();
    const topic = http.expectOne('/api/topics');
    expect(topic.request.body).toEqual({ name: 'Operations' });
    topic.flush({ id: 'topic-1', name: 'Operations' });

    service.subscribe('topic-1', 'user-1', 'High').subscribe();
    const subscription = http.expectOne('/api/topics/topic-1/subscribers');
    expect(subscription.request.body).toEqual({ userId: 'user-1', minimumImportance: 'High' });
    subscription.flush(null);

    service.publish('topic-1', 'Incident', 'Investigate', 'Critical', 'ops-1').subscribe();
    const publish = http.expectOne('/api/topics/topic-1/messages');
    expect(publish.request.method).toBe('POST');
    expect(publish.request.headers.get('Idempotency-Key')).toBe('ops-1');
    expect(publish.request.body).toEqual({
      title: 'Incident',
      body: 'Investigate',
      importance: 'Critical',
    });
    publish.flush({ messageId: 'message-1', idempotencyReplayed: false });
  });

  it('uses the delivery, inbox, archive, read, dead-letter and retry resource shapes', () => {
    const service = TestBed.inject(NotificationApiService);
    const http = TestBed.inject(HttpTestingController);

    service.getDelivery('message-1').subscribe();
    expect(http.expectOne('/api/deliveries/message-1').request.method).toBe('GET');
    service.getInbox('user-1').subscribe();
    expect(http.expectOne('/api/users/user-1/messages').request.method).toBe('GET');
    service.getArchive().subscribe();
    expect(http.expectOne('/api/archive').request.method).toBe('GET');
    service.getDeadLetters().subscribe();
    expect(http.expectOne('/api/deliveries/dead-letter').request.method).toBe('GET');
    service.markRead('user-1', 'message-1').subscribe();
    expect(http.expectOne('/api/users/user-1/messages/message-1/read').request.method).toBe('PUT');
    service.retry('delivery-1').subscribe();
    expect(http.expectOne('/api/deliveries/delivery-1/retry').request.method).toBe('POST');

    http.match(() => true).forEach((request) => request.flush(null));
  });

  it('maps validation Problem Details before generic transport errors', () => {
    const validationError = new HttpErrorResponse({
      status: 400,
      error: { title: 'Validation failed', errors: { AlertKeywords: ['Use unique keywords.'] } },
    });
    const unavailable = new HttpErrorResponse({ status: 0, error: null });

    expect(problemDetail(validationError)).toBe('Use unique keywords.');
    expect(problemDetail(unavailable)).toBe('The notification service is unavailable.');
  });
});
