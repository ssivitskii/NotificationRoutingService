import { CommonModule } from '@angular/common';
import { Component, InjectionToken, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Observable,
  Subscription,
  concatMap,
  forkJoin,
  map,
  of,
  switchMap,
  tap,
  timer,
} from 'rxjs';
import { NotificationApiService, problemDetail } from './notification-api.service';
import {
  DeadLetterResponse,
  DeliveryResponse,
  DispatchStatus,
  Importance,
  MessageResponse,
} from './notification.models';

export const DELIVERY_POLL_INTERVAL = new InjectionToken<number>('DELIVERY_POLL_INTERVAL', {
  providedIn: 'root',
  factory: () => 600,
});
export const DELIVERY_POLL_LIMIT = new InjectionToken<number>('DELIVERY_POLL_LIMIT', {
  providedIn: 'root',
  factory: () => 40,
});

type SetupState = 'idle' | 'loading' | 'ready' | 'error';
type TrackingState = 'idle' | 'loading' | 'tracking' | 'success' | 'error';
type PanelState = 'idle' | 'loading' | 'ready' | 'error';
type StepState = 'waiting' | 'active' | 'done';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnDestroy {
  private readonly api = inject(NotificationApiService);
  private readonly pollInterval = inject(DELIVERY_POLL_INTERVAL);
  private readonly pollLimit = inject(DELIVERY_POLL_LIMIT);
  private setupSubscription?: Subscription;
  private publishSubscription?: Subscription;
  private pollSubscription?: Subscription;
  private refreshSubscription?: Subscription;
  private actionSubscription?: Subscription;
  private setupEpoch = 0;
  private publishEpoch = 0;
  private dataEpoch = 0;
  private pollCount = 0;

  readonly importanceOptions: Importance[] = ['Low', 'Normal', 'High', 'Critical'];
  readonly terminalStatuses: DispatchStatus[] = ['Succeeded', 'PartiallyFailed', 'DeadLettered'];
  readonly setupState = signal<SetupState>('idle');
  readonly trackingState = signal<TrackingState>('idle');
  readonly panelState = signal<PanelState>('idle');
  readonly setupError = signal<string | null>(null);
  readonly trackingError = signal<string | null>(null);
  readonly panelError = signal<string | null>(null);
  readonly setupValidation = signal<string | null>(null);
  readonly publishValidation = signal<string | null>(null);
  readonly userStep = signal<StepState>('waiting');
  readonly topicStep = signal<StepState>('waiting');
  readonly subscriptionStep = signal<StepState>('waiting');
  readonly userId = signal<string | null>(null);
  readonly topicId = signal<string | null>(null);
  readonly delivery = signal<DeliveryResponse | null>(null);
  readonly inbox = signal<MessageResponse[]>([]);
  readonly archive = signal<MessageResponse[]>([]);
  readonly deadLetters = signal<DeadLetterResponse[]>([]);
  readonly actionMessage = signal<string | null>(null);
  readonly acceptedIdempotencyKey = signal<string | null>(null);
  readonly idempotencyReplayed = signal(false);

  userName = '';
  alertKeywords = 'security, outage, latency';
  topicName = '';
  minimumImportance: Importance = 'High';
  messageTitle = 'Latency threshold exceeded';
  messageBody = 'API latency crossed the production SLO. Triage is required.';
  messageImportance: Importance = 'Critical';
  idempotencyKey = this.createIdempotencyKey();

  constructor() {
    this.applyFreshWorkspaceNames();
  }

  ngOnDestroy(): void {
    this.cancelAll();
  }

  runSetup(): void {
    if (this.setupState() === 'ready' || this.setupState() === 'loading') return;
    const name = this.userName.trim();
    const topic = this.topicName.trim();
    const keywords = this.parseKeywords();
    const validation = this.validateSetup(name, topic, keywords);
    this.setupValidation.set(validation);
    if (validation) return;

    this.cancelSetup();
    this.cancelTracking();
    this.cancelRefresh();
    this.actionSubscription?.unsubscribe();
    const epoch = ++this.setupEpoch;
    this.publishEpoch++;
    this.dataEpoch++;
    this.setupState.set('loading');
    this.trackingState.set('idle');
    this.panelState.set('idle');
    this.setupError.set(null);
    this.trackingError.set(null);
    this.panelError.set(null);
    this.publishValidation.set(null);
    this.actionMessage.set(null);
    this.delivery.set(null);
    this.inbox.set([]);
    this.archive.set([]);
    this.deadLetters.set([]);
    if (!this.userId()) this.userStep.set('active');
    if (!this.topicId()) this.topicStep.set('waiting');
    this.subscriptionStep.set('waiting');

    this.setupSubscription = this.getOrCreateUser(name, keywords, epoch)
      .pipe(
        concatMap((userId) =>
          this.getOrCreateTopic(topic, epoch).pipe(
            concatMap((topicId) => {
              if (epoch === this.setupEpoch) this.subscriptionStep.set('active');
              return this.api.subscribe(topicId, userId, this.minimumImportance);
            }),
          ),
        ),
      )
      .subscribe({
        complete: () => {
          if (epoch !== this.setupEpoch) return;
          this.subscriptionStep.set('done');
          this.setupState.set('ready');
          this.refreshAll();
        },
        error: (error: unknown) => {
          if (epoch !== this.setupEpoch) return;
          this.setupState.set('error');
          this.setupError.set(problemDetail(error));
          this.setActiveStepToWaiting();
        },
      });
  }

  publish(): void {
    if (this.trackingState() === 'loading' || this.trackingState() === 'tracking') return;
    const topicId = this.topicId();
    const title = this.messageTitle.trim();
    const body = this.messageBody.trim();
    const key = this.idempotencyKey.trim();
    const validation = this.validatePublication(topicId, title, body, key);
    this.publishValidation.set(validation);
    if (validation || !topicId) return;

    this.cancelTracking();
    const epoch = ++this.publishEpoch;
    this.trackingState.set('loading');
    this.trackingError.set(null);
    this.actionMessage.set(null);
    this.acceptedIdempotencyKey.set(null);
    this.idempotencyReplayed.set(false);
    this.delivery.set(null);
    this.publishSubscription = this.api
      .publish(topicId, title, body, this.messageImportance, key)
      .subscribe({
        next: (accepted) => {
          if (epoch !== this.publishEpoch) return;
          this.acceptedIdempotencyKey.set(key);
          this.idempotencyReplayed.set(accepted.idempotencyReplayed);
          this.trackingState.set('tracking');
          this.startPolling(accepted.messageId, epoch);
        },
        error: (error: unknown) => {
          if (epoch !== this.publishEpoch) return;
          this.trackingState.set('error');
          this.trackingError.set(problemDetail(error));
        },
      });
  }

  reset(): void {
    this.cancelAll();
    this.setupEpoch++;
    this.publishEpoch++;
    this.dataEpoch++;
    this.setupState.set('idle');
    this.trackingState.set('idle');
    this.panelState.set('idle');
    this.setupError.set(null);
    this.trackingError.set(null);
    this.panelError.set(null);
    this.setupValidation.set(null);
    this.publishValidation.set(null);
    this.userStep.set('waiting');
    this.topicStep.set('waiting');
    this.subscriptionStep.set('waiting');
    this.userId.set(null);
    this.topicId.set(null);
    this.delivery.set(null);
    this.inbox.set([]);
    this.archive.set([]);
    this.deadLetters.set([]);
    this.actionMessage.set(null);
    this.acceptedIdempotencyKey.set(null);
    this.idempotencyReplayed.set(false);
    this.idempotencyKey = this.createIdempotencyKey();
    this.applyFreshWorkspaceNames();
  }

  refreshAll(): void {
    const userId = this.userId();
    if (!userId) return;
    this.cancelRefresh();
    const epoch = ++this.dataEpoch;
    this.panelState.set('loading');
    this.panelError.set(null);
    this.refreshSubscription = forkJoin({
      inbox: this.api.getInbox(userId),
      archive: this.api.getArchive(),
      deadLetters: this.api.getDeadLetters(),
    }).subscribe({
      next: ({ inbox, archive, deadLetters }) => {
        if (epoch !== this.dataEpoch) return;
        this.inbox.set(inbox);
        this.archive.set(archive);
        this.deadLetters.set(deadLetters);
        this.panelState.set('ready');
      },
      error: (error: unknown) => {
        if (epoch !== this.dataEpoch) return;
        this.panelState.set('error');
        this.panelError.set(problemDetail(error));
      },
    });
  }

  markRead(messageId: string): void {
    const userId = this.userId();
    if (!userId) return;
    this.actionSubscription?.unsubscribe();
    this.actionMessage.set('Updating inbox…');
    this.actionSubscription = this.api.markRead(userId, messageId).subscribe({
      next: () => {
        this.actionMessage.set('Message marked as read.');
        this.refreshAll();
      },
      error: (error: unknown) => this.actionMessage.set(problemDetail(error)),
    });
  }

  retry(deadLetter: DeadLetterResponse): void {
    this.actionSubscription?.unsubscribe();
    this.cancelTracking();
    const epoch = ++this.publishEpoch;
    this.actionMessage.set('Retry accepted; tracking delivery…');
    this.trackingState.set('loading');
    this.trackingError.set(null);
    this.actionSubscription = this.api.retry(deadLetter.delivery.id).subscribe({
      next: () => {
        if (epoch !== this.publishEpoch) return;
        this.trackingState.set('tracking');
        this.startPolling(deadLetter.messageId, epoch);
      },
      error: (error: unknown) => {
        if (epoch !== this.publishEpoch) return;
        this.trackingState.set('error');
        this.trackingError.set(problemDetail(error));
      },
    });
  }

  statusTone(status: string): string {
    if (status === 'Succeeded' || status === 'Read') return 'positive';
    if (status === 'DeadLettered' || status === 'PartiallyFailed') return 'negative';
    if (status === 'Processing' || status === 'RetryScheduled') return 'active';
    return 'neutral';
  }

  trackById(_index: number, item: { id: string }): string {
    return item.id;
  }

  private startPolling(messageId: string, epoch: number): void {
    this.pollSubscription?.unsubscribe();
    this.pollCount = 0;
    this.pollSubscription = timer(0, this.pollInterval)
      .pipe(switchMap(() => this.api.getDelivery(messageId)))
      .subscribe({
        next: (delivery) => {
          if (epoch !== this.publishEpoch) return;
          this.pollCount++;
          this.delivery.set(delivery);
          if (this.terminalStatuses.includes(delivery.status)) {
            this.pollSubscription?.unsubscribe();
            this.trackingState.set('success');
            this.actionMessage.set(`Dispatch reached ${delivery.status}.`);
            this.idempotencyKey = this.createIdempotencyKey();
            this.refreshAll();
          } else if (this.pollCount >= this.pollLimit) {
            this.pollSubscription?.unsubscribe();
            this.trackingState.set('error');
            this.trackingError.set('Tracking timed out. Refresh the delivery or publish again.');
          }
        },
        error: (error: unknown) => {
          if (epoch !== this.publishEpoch) return;
          this.trackingState.set('error');
          this.trackingError.set(problemDetail(error));
        },
      });
  }

  private parseKeywords(): string[] {
    return this.alertKeywords
      .split(',')
      .map((keyword) => keyword.trim())
      .filter(Boolean);
  }

  private getOrCreateUser(name: string, keywords: string[], epoch: number): Observable<string> {
    const existingUserId = this.userId();
    if (existingUserId) return of(existingUserId);

    return this.api.createUser(name, keywords).pipe(
      tap((user) => {
        if (epoch !== this.setupEpoch) return;
        this.userId.set(user.id);
        this.userStep.set('done');
        this.topicStep.set('active');
      }),
      map((user) => user.id),
    );
  }

  private getOrCreateTopic(name: string, epoch: number): Observable<string> {
    const existingTopicId = this.topicId();
    if (existingTopicId) return of(existingTopicId);

    if (epoch === this.setupEpoch) this.topicStep.set('active');
    return this.api.createTopic(name).pipe(
      tap((topic) => {
        if (epoch !== this.setupEpoch) return;
        this.topicId.set(topic.id);
        this.topicStep.set('done');
      }),
      map((topic) => topic.id),
    );
  }

  private validateSetup(name: string, topic: string, keywords: string[]): string | null {
    if (!name) return 'Enter an operator name.';
    if (name.length > 100) return 'Operator name must be at most 100 characters.';
    if (!topic) return 'Enter a topic name.';
    if (topic.length > 100) return 'Topic name must be at most 100 characters.';
    if (keywords.length > 10) return 'Use at most 10 alert keywords.';
    if (keywords.some((keyword) => keyword.length > 50))
      return 'Each alert keyword must be at most 50 characters.';
    if (new Set(keywords.map((keyword) => keyword.toLocaleLowerCase())).size !== keywords.length)
      return 'Alert keywords must be unique.';
    return null;
  }

  private validatePublication(
    topicId: string | null,
    title: string,
    body: string,
    key: string,
  ): string | null {
    if (!topicId) return 'Complete workspace setup first.';
    if (!title || title.length > 200) return 'Title must contain 1–200 characters.';
    if (!body || body.length > 4000) return 'Body must contain 1–4000 characters.';
    if (!key || key.length > 128) return 'Idempotency key must contain 1–128 characters.';
    return null;
  }

  private setActiveStepToWaiting(): void {
    if (this.userStep() === 'active') this.userStep.set('waiting');
    if (this.topicStep() === 'active') this.topicStep.set('waiting');
    if (this.subscriptionStep() === 'active') this.subscriptionStep.set('waiting');
  }

  private createIdempotencyKey(): string {
    return `ops-${globalThis.crypto.randomUUID()}`;
  }

  private applyFreshWorkspaceNames(): void {
    const suffix = globalThis.crypto.randomUUID().slice(0, 8);
    this.userName = `Maya Chen ${suffix}`;
    this.topicName = `Platform Operations ${suffix}`;
  }

  private cancelSetup(): void {
    this.setupSubscription?.unsubscribe();
  }

  private cancelTracking(): void {
    this.publishSubscription?.unsubscribe();
    this.pollSubscription?.unsubscribe();
  }

  private cancelRefresh(): void {
    this.refreshSubscription?.unsubscribe();
  }

  private cancelAll(): void {
    this.cancelSetup();
    this.cancelTracking();
    this.cancelRefresh();
    this.actionSubscription?.unsubscribe();
  }
}
