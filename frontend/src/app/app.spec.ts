import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { App, DELIVERY_POLL_INTERVAL, DELIVERY_POLL_LIMIT } from './app';
import { NotificationApiService } from './notification-api.service';
import {
  DeadLetterResponse,
  DeliveryResponse,
  MessageResponse,
  PublishAcceptedResponse,
  TopicResponse,
  UserResponse,
} from './notification.models';

const message: MessageResponse = {
  id: 'message-1',
  title: 'Incident',
  body: 'Investigate',
  importance: 'Critical',
  createdAt: '2026-01-01T10:00:00Z',
  status: 'Unread',
};

const queued: DeliveryResponse = {
  messageId: 'message-1',
  topicId: 'topic-1',
  title: 'Incident',
  importance: 'Critical',
  acceptedAt: '2026-01-01T10:00:00Z',
  status: 'Queued',
  deliveries: [],
};

class MockNotificationApi {
  createUser = vi.fn<() => Observable<UserResponse>>(() =>
    of({ id: 'user-1', name: 'Maya', alertKeywords: [], webhookUrl: null }),
  );
  createTopic = vi.fn<() => Observable<TopicResponse>>(() =>
    of({ id: 'topic-1', name: 'Operations' }),
  );
  subscribe = vi.fn<() => Observable<void>>(() => of(undefined));
  publish = vi.fn<() => Observable<PublishAcceptedResponse>>(() =>
    of({ messageId: 'message-1', idempotencyReplayed: false }),
  );
  getDelivery = vi.fn<() => Observable<DeliveryResponse>>(() => of(queued));
  getInbox = vi.fn<() => Observable<MessageResponse[]>>(() => of([]));
  getArchive = vi.fn<() => Observable<MessageResponse[]>>(() => of([]));
  getDeadLetters = vi.fn<() => Observable<DeadLetterResponse[]>>(() => of([]));
  markRead = vi.fn<() => Observable<void>>(() => of(undefined));
  retry = vi.fn<() => Observable<void>>(() => of(undefined));
}

describe('App', () => {
  let api: MockNotificationApi;
  let fixture: ComponentFixture<App>;
  let component: App;

  beforeEach(async () => {
    api = new MockNotificationApi();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: NotificationApiService, useValue: api },
        { provide: DELIVERY_POLL_INTERVAL, useValue: 10 },
        { provide: DELIVERY_POLL_LIMIT, useValue: 3 },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(App);
    component = fixture.componentInstance;
    component.userName = 'Maya Chen';
    component.topicName = 'Platform Operations';
  });

  afterEach(() => {
    fixture.destroy();
    vi.useRealTimers();
  });

  it('creates user, topic, then subscription in sequence', () => {
    const user = new Subject<UserResponse>();
    const topic = new Subject<TopicResponse>();
    const subscription = new Subject<void>();
    api.createUser.mockReturnValue(user);
    api.createTopic.mockReturnValue(topic);
    api.subscribe.mockReturnValue(subscription);

    component.runSetup();
    expect(api.createUser).toHaveBeenCalledWith('Maya Chen', ['security', 'outage', 'latency']);
    expect(api.createTopic).not.toHaveBeenCalled();
    user.next({ id: 'user-1', name: 'Maya', alertKeywords: [], webhookUrl: null });
    user.complete();
    expect(api.createTopic).toHaveBeenCalledWith('Platform Operations');
    expect(api.subscribe).not.toHaveBeenCalled();
    topic.next({ id: 'topic-1', name: 'Operations' });
    topic.complete();
    expect(api.subscribe).toHaveBeenCalledWith('topic-1', 'user-1', 'High');
    subscription.next();
    subscription.complete();
    expect(component.setupState()).toBe('ready');
    expect(component.subscriptionStep()).toBe('done');
  });

  it('blocks invalid duplicate keywords before calling the API', () => {
    component.alertKeywords = 'security, SECURITY';
    component.runSetup();
    expect(component.setupValidation()).toBe('Alert keywords must be unique.');
    expect(api.createUser).not.toHaveBeenCalled();
  });

  it('does not repeat setup after the workspace is ready', () => {
    component.runSetup();
    component.runSetup();

    expect(api.createUser).toHaveBeenCalledTimes(1);
    expect(api.createTopic).toHaveBeenCalledTimes(1);
    expect(api.subscribe).toHaveBeenCalledTimes(1);
  });

  it('resumes at topic creation after user creation succeeded', () => {
    api.createTopic.mockReturnValueOnce(throwError(() => new Error('topic unavailable')));
    component.runSetup();
    expect(component.setupState()).toBe('error');
    expect(component.userId()).toBe('user-1');
    expect(component.userStep()).toBe('done');
    expect(component.topicId()).toBeNull();

    component.runSetup();

    expect(api.createUser).toHaveBeenCalledTimes(1);
    expect(api.createTopic).toHaveBeenCalledTimes(2);
    expect(api.subscribe).toHaveBeenCalledTimes(1);
    expect(component.setupState()).toBe('ready');
  });

  it('reset creates fresh workspace names that reconnect without reusing identifiers', () => {
    component.runSetup();
    const previousUserName = component.userName;
    const previousTopicName = component.topicName;

    component.reset();

    expect(component.userName).not.toBe(previousUserName);
    expect(component.topicName).not.toBe(previousTopicName);
    expect(component.userId()).toBeNull();
    component.runSetup();
    expect(api.createUser).toHaveBeenLastCalledWith(component.userName, [
      'security',
      'outage',
      'latency',
    ]);
    expect(api.createTopic).toHaveBeenLastCalledWith(component.topicName);
  });

  it('cancels an obsolete setup when reset starts a replacement', () => {
    const firstUser = new Subject<UserResponse>();
    api.createUser.mockReturnValueOnce(firstUser);
    component.runSetup();
    component.reset();
    component.userName = 'Replacement Operator';
    component.runSetup();

    firstUser.next({ id: 'stale-user', name: 'Stale', alertKeywords: [], webhookUrl: null });
    firstUser.complete();

    expect(api.createUser).toHaveBeenLastCalledWith('Replacement Operator', [
      'security',
      'outage',
      'latency',
    ]);
    expect(component.userId()).toBe('user-1');
    expect(component.setupState()).toBe('ready');
  });

  it('publishes, observes an intermediate state, then refreshes all terminal records', async () => {
    vi.useFakeTimers();
    component.runSetup();
    api.getDelivery
      .mockReturnValueOnce(of(queued))
      .mockReturnValueOnce(of({ ...queued, status: 'Processing' }))
      .mockReturnValueOnce(
        of({
          ...queued,
          status: 'Succeeded',
          deliveries: [
            {
              id: 'delivery-1',
              destination: 'inbox:user-1',
              status: 'Succeeded',
              attempts: [],
              lastError: null,
              lastHttpStatusCode: null,
            },
          ],
        }),
      );
    api.getInbox.mockReturnValue(of([message]));
    api.getArchive.mockReturnValue(of([message]));

    component.publish();
    expect(api.publish).toHaveBeenCalledWith(
      'topic-1',
      'Latency threshold exceeded',
      'API latency crossed the production SLO. Triage is required.',
      'Critical',
      component.idempotencyKey,
    );
    await vi.advanceTimersByTimeAsync(0);
    expect(component.delivery()?.status).toBe('Queued');
    await vi.advanceTimersByTimeAsync(10);
    expect(component.delivery()?.status).toBe('Processing');
    await vi.advanceTimersByTimeAsync(10);
    expect(component.trackingState()).toBe('success');
    expect(component.inbox()).toEqual([message]);
    expect(component.archive()).toEqual([message]);
    expect(api.getDeadLetters).toHaveBeenCalled();
  });

  it('ignores a stale publish response after reset and cancels tracking on destroy', async () => {
    vi.useFakeTimers();
    component.runSetup();
    const accepted = new Subject<PublishAcceptedResponse>();
    api.publish.mockReturnValue(accepted);
    component.publish();
    component.reset();
    accepted.next({ messageId: 'late-message', idempotencyReplayed: false });
    accepted.complete();
    await vi.advanceTimersByTimeAsync(20);
    expect(api.getDelivery).not.toHaveBeenCalled();
    expect(component.trackingState()).toBe('idle');

    component.runSetup();
    component.publish();
    fixture.destroy();
    await vi.advanceTimersByTimeAsync(30);
    expect(api.getDelivery).not.toHaveBeenCalled();
  });

  it('stops polling with an explicit timeout state', async () => {
    vi.useFakeTimers();
    component.runSetup();
    component.publish();
    await vi.advanceTimersByTimeAsync(30);
    expect(api.getDelivery).toHaveBeenCalledTimes(3);
    expect(component.trackingState()).toBe('error');
    expect(component.trackingError()).toContain('timed out');
  });

  it('rotates UUID idempotency keys only after terminal delivery and uses the fresh key next', async () => {
    vi.useFakeTimers();
    component.runSetup();
    const firstKey = component.idempotencyKey;
    api.getDelivery.mockReturnValue(of({ ...queued, status: 'Succeeded' }));

    component.publish();
    await vi.advanceTimersByTimeAsync(0);

    const secondKey = component.idempotencyKey;
    expect(component.acceptedIdempotencyKey()).toBe(firstKey);
    expect(firstKey).toMatch(/^ops-[0-9a-f-]{36}$/);
    expect(secondKey).not.toBe(firstKey);
    component.publish();
    await vi.advanceTimersByTimeAsync(0);
    expect(api.publish).toHaveBeenNthCalledWith(
      2,
      'topic-1',
      'Latency threshold exceeded',
      'API latency crossed the production SLO. Triage is required.',
      'Critical',
      secondKey,
    );
  });

  it('blocks a second publish while tracking and keeps the key when publication fails', () => {
    component.runSetup();
    const delivery = new Subject<DeliveryResponse>();
    api.getDelivery.mockReturnValue(delivery);
    component.publish();
    component.publish();
    expect(api.publish).toHaveBeenCalledTimes(1);

    component.reset();
    component.runSetup();
    const retryKey = component.idempotencyKey;
    api.publish.mockReturnValue(throwError(() => new Error('offline')));
    component.publish();
    expect(component.idempotencyKey).toBe(retryKey);
    expect(component.trackingState()).toBe('error');
  });

  it('delegates mark-read and dead-letter retry to the API service', () => {
    component.runSetup();
    component.markRead('message-1');
    expect(api.markRead).toHaveBeenCalledWith('user-1', 'message-1');
    const deadLetter: DeadLetterResponse = {
      messageId: 'message-1',
      topicId: 'topic-1',
      delivery: {
        id: 'delivery-1',
        destination: 'webhook:user-1',
        status: 'DeadLettered',
        attempts: [],
        lastError: 'Rejected',
        lastHttpStatusCode: 400,
      },
    };
    component.retry(deadLetter);
    expect(api.retry).toHaveBeenCalledWith('delivery-1');
  });
});
