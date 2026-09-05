export type Importance = 'Low' | 'Normal' | 'High' | 'Critical';
export type DispatchStatus =
  'Queued' | 'Processing' | 'Succeeded' | 'PartiallyFailed' | 'DeadLettered';
export type DeliveryStatus =
  'Queued' | 'Processing' | 'RetryScheduled' | 'Succeeded' | 'DeadLettered';
export type ReadStatus = 'Unread' | 'Read';

export interface UserResponse {
  id: string;
  name: string;
  alertKeywords: string[];
  webhookUrl: string | null;
}

export interface TopicResponse {
  id: string;
  name: string;
}

export interface MessageResponse {
  id: string;
  title: string;
  body: string;
  importance: Importance;
  createdAt: string;
  status: ReadStatus | null;
}

export interface DeliveryAttempt {
  number: number;
  startedAt: string;
  completedAt: string;
  succeeded: boolean;
  retryable: boolean;
  httpStatusCode: number | null;
  error: string | null;
}

export interface DeliveryTarget {
  id: string;
  destination: string;
  status: DeliveryStatus;
  attempts: DeliveryAttempt[];
  lastError: string | null;
  lastHttpStatusCode: number | null;
}

export interface DeliveryResponse {
  messageId: string;
  topicId: string;
  title: string;
  importance: Importance;
  acceptedAt: string;
  status: DispatchStatus;
  deliveries: DeliveryTarget[];
}

export interface DeadLetterResponse {
  messageId: string;
  topicId: string;
  delivery: DeliveryTarget;
}

export interface PublishAcceptedResponse {
  messageId: string;
  idempotencyReplayed: boolean;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
