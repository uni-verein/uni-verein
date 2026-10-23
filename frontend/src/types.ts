import { UUIDTypes } from 'uuid';
import { AlertColor } from '@mui/material/Alert';
import React from 'react';

export interface MemberPayload {
  id?: string;
  memberNumber?: number;
  gender?: Gender | string;
  firstName?: string;
  middleName?: string;
  lastName?: string;
  birthday?: string | null;
  street?: string;
  postalCode?: string;
  city?: string;
  countryCode?: string | null;
  email?: string;
  phone?: string;
  bulkMail?: BulkMail | string | null;
  startOfStudies?: string | null;
  endOfStudies?: string | null;
  academicDegree?: AcademicDegree | null;
  courseOfStudy?: string;
  taskWithinTheClub?: TaskWithinTheClub | string;
  memberCategoryId?: string | null;
  iban?: string;
  bic?: string;
  sepaConsent?: string | null;
  entryDate?: string | null;
  exitDate?: string | null;
  contributionPlanId?: string | null;
}

export interface MemberApiResult extends MemberPayload {
  id: string;
  memberNumber: number;
  email: string;
}

export interface ReceiptCategoryApiResult {
  id: string;
  name: string;
}

interface ReceiptFileApiResult {
  id: string;
  contentType: string;
  position: number;
}

export interface ReceiptApiResult {
  id: string;
  userId: string;
  userName: string | null;
  amount: number;
  receiptDate: string;
  categoryId: string | null;
  categoryName: string | null;
  vendor: string | null;
  description: string | null;
  paymentMethod: string | null;
  paid: boolean;
  files: ReceiptFileApiResult[];
  createdAt: string;
  deletedAt: string | null;
}

export interface ConfigContextType {
  config: { pageName: string; logo: string; selfEnrollmentEnabled: boolean };
  loading: boolean;
  reloadConfig: () => Promise<void>;
  serverReachable: boolean;
}

export interface UserRoleProps {
  role: Role | string;
}

export interface UserManagementProps {
  userId?: UUIDTypes;
  accountView: boolean;
  role?: Role | string;
}

export interface Member {
  id: UUIDTypes;
  memberNumber: number;
  gender: Gender;
  firstName: string;
  middleName: string;
  lastName: string;
  birthday: Date | null;
  street: string;
  postalCode: string;
  city: string;
  countryCode: string | null;
  email: string;
  phone: string;
  bulkMail: BulkMail | null;
  startOfStudies: Date | null;
  endOfStudies: Date | null;
  academicDegree: AcademicDegree | null;
  courseOfStudy: string;
  taskWithinTheClub: TaskWithinTheClub;
  membershipStatus?: MembershipStatus;
  memberCategoryId: UUIDTypes | null;
  iban: string;
  bic: string;
  sepaConsent: Date | null;
  entryDate: Date | null;
  exitDate: Date | null;
  contributionPlanId: UUIDTypes | null;
  deletedAt: Date | null;
}

export interface PendingSelfEnrollment {
  id: UUIDTypes;
  gender: Gender;
  firstName: string;
  middleName: string;
  lastName: string;
  email: string;
  memberCategoryId: UUIDTypes | null;
  memberCategoryName: string | null;
  contributionPlanId: UUIDTypes | null;
  submittedAt: string;
  submittedIp: string;
  confirmedAt: string | null;
}

export interface PendingSelfEnrollmentDetail {
  id: UUIDTypes;
  gender: Gender;
  firstName: string;
  middleName: string;
  lastName: string;
  birthday: Date | null;
  street: string;
  postalCode: string;
  city: string;
  countryCode: string | null;
  email: string;
  phone: string;
  bulkMail: BulkMail | null;
  startOfStudies: Date | null;
  endOfStudies: Date | null;
  academicDegree: AcademicDegree | null;
  courseOfStudy: string;
  motivation: string;
  memberCategoryId: UUIDTypes | null;
  memberCategoryName: string | null;
  iban: string;
  bic: string;
  sepaConsent: string | null;
  contributionPlanId: UUIDTypes | null;
  submittedAt: string;
  submittedIp: string;
  confirmedAt: string | null;
}

export interface MemberErrors {
  birthday?: string;
  city?: string;
  contributionPlanId?: string;
  countryCode?: string;
  courseOfStudy?: string;
  email?: string;
  endOfStudies?: string;
  entryDate?: string;
  exitDate?: string;
  firstName?: string;
  gender?: string;
  iban?: string;
  bic?: string;
  lastName?: string;
  memberCategoryId?: string;
  memberNumber?: string;
  middleName?: string;
  motivation?: string;
  postalCode?: string;
  sepaConsent?: string;
  startOfStudies?: string;
  street?: string;
}

export interface User {
  id: UUIDTypes;
  username: string;
  email: string | null;
  password: string;
  role: Role;
}

export interface ContributionPlans {
  id: UUIDTypes;
  name: string;
  amount: number;
  interval: Interval;
}

export interface MemberCategory {
  id: UUIDTypes;
  category: string;
  name: string;
}

export interface ReceiptCategory {
  id: UUIDTypes;
  name: string;
}

export interface ReceiptFile {
  id: UUIDTypes;
  contentType: string;
  position: number;
}

export interface Receipt {
  id: UUIDTypes;
  userId: UUIDTypes;
  userName: string | null;
  amount: number;
  receiptDate: Date;
  categoryId: UUIDTypes | null;
  categoryName: string | null;
  vendor: string | null;
  description: string | null;
  paymentMethod: ReceiptPaymentMethod | null;
  paid: boolean;
  files: ReceiptFile[];
  createdAt: Date;
  deletedAt: Date | null;
}

export enum ReceiptPaymentMethod {
  CASH = 'CASH',
  BANK_TRANSFER = 'BANK_TRANSFER',
  CARD = 'CARD',
  SEPA_DIRECT_DEBIT = 'SEPA_DIRECT_DEBIT',
  PAYPAL = 'PAYPAL',
  OTHER = 'OTHER',
}

export enum Interval {
  YEARLY = 'YEARLY',
  MONTHLY = 'MONTHLY',
}

export enum Role {
  ADMIN = 'ADMIN',
  USER = 'USER',
  FINANCIAL_MANAGER = 'FINANCIAL_MANAGER',
}

export enum UserSettingType {
  RECEIPT_NOTIFICATION = 'RECEIPT_NOTIFICATION',
  SELF_ENROLLMENT_NOTIFICATION = 'SELF_ENROLLMENT_NOTIFICATION',
  CALENDAR_INVITE_EMAIL = 'CALENDAR_INVITE_EMAIL',
  CALENDAR_CHANGE_EMAIL = 'CALENDAR_CHANGE_EMAIL',
  CALENDAR_REMINDER_EMAIL = 'CALENDAR_REMINDER_EMAIL',
}

export interface UserSetting {
  type: UserSettingType;
  enabled: boolean;
}

export enum TaskWithinTheClub {
  MEMBER = 'MEMBER',
  CHAIRMAN = 'CHAIRMAN',
  SECOND_CHAIRMAN = 'SECOND_CHAIRMAN',
  JUNIOR_BOARD_MEMBER = 'JUNIOR_BOARD_MEMBER',
  CHIEF_FINANCE_OFFICER = 'CHIEF_FINANCE_OFFICER',
  WEBSITE_MANAGER = 'WEBSITE_MANAGER',
  ALUMNI_OFFICER = 'ALUMNI_OFFICER',
  STUDENT_COUNCIL_REPRESENTATIVE = 'STUDENT_COUNCIL_REPRESENTATIVE',
}

export enum MembershipStatus {
  MEMBER = 'MEMBER',
  SUPPORTING_MEMBER = 'SUPPORTING_MEMBER',
}

export enum Gender {
  MALE = 'MALE',
  FEMALE = 'FEMALE',
  DIVERSE = 'DIVERSE',
}

export enum BulkMail {
  ALLOWED = 'ALLOWED',
  NOT_ALLOWED = 'NOT_ALLOWED',
}

export enum AcademicDegree {
  BA = 'BA',
  BSC = 'BSC',
  BENG = 'BENG',
  LLB = 'LLB',
  BED = 'BED',
  BBA = 'BBA',
  BFA = 'BFA',
  BMUS = 'BMUS',
  BARCH = 'BARCH',
  BN = 'BN',
  BSW = 'BSW',
  BTH = 'BTH',
  BPHIL = 'BPHIL',
  BCS = 'BCS',
  BEC = 'BEC',
  MA = 'MA',
  MSC = 'MSC',
  MENG = 'MENG',
  LLM = 'LLM',
  MED = 'MED',
  MBA = 'MBA',
  MFA = 'MFA',
  MMUS = 'MMUS',
  MARCH = 'MARCH',
  MPH = 'MPH',
  MSW = 'MSW',
  MPA = 'MPA',
  MPHIL = 'MPHIL',
  MTH = 'MTH',
  MCS = 'MCS',
  MEC = 'MEC',
  MFIN = 'MFIN',
  MIR = 'MIR',
  MRES = 'MRES',
  PHD = 'PHD',
  MD = 'MD',
  LLD = 'LLD',
  DSC = 'DSC',
  DENG = 'DENG',
  EDD = 'EDD',
  DBA = 'DBA',
  DTH = 'DTH',
  DFA = 'DFA',
  DMUS = 'DMUS',
  DRPH = 'DRPH',
  PSYD = 'PSYD',
  DARCH = 'DARCH',
  DNP = 'DNP',
  DSW = 'DSW',
  JD = 'JD',
  DR = 'DR',
  HABIL = 'HABIL',
  DRHABIL = 'DRHABIL',
  DRHC = 'DRHC',
  DRHCMULT = 'DRHCMULT',
  DIPLOM = 'DIPLOM',
  MAGISTER = 'MAGISTER',
  STAATSEXAMEN = 'STAATSEXAMEN',
  LICENCE = 'LICENCE',
  MAITRISE = 'MAITRISE',
  INGENIEUR = 'INGENIEUR',
  LAUREA = 'LAUREA',
  LAUREAMAGISTRALE = 'LAUREAMAGISTRALE',
  LICENCIATURA = 'LICENCIATURA',
  TITULODEGRADO = 'TITULODEGRADO',
  KANDIDATVIED = 'KANDIDATVIED',
  DOCENT = 'DOCENT',
}

export interface SnackbarState {
  status: null | AlertColor;
  message: string;
}

export interface Recipient {
  email: string;
  firstName: string;
  lastName: string;
}

export interface RecipientListProps {
  recipients: Recipient[];
  selectedEmails: string[];
  onChange: (emails: string[] | ((prev: string[]) => string[])) => void;
  filter: Filter;
  onFilter: (filter: Filter | ((prev: Filter) => Filter)) => void;
  totalCount: number;
  memberCategories: MemberCategory[];
}

export interface Filter {
  categoryId: UUIDTypes;
  offset: number;
  limit: number;
  name?: string;
}

export interface ProgressData {
  email: string;
  success: boolean;
  errorMessage?: string;
}

export interface EmailResult {
  email: string;
  success: boolean;
  errorMessage?: string;
}

export interface SummaryData {
  total: number;
  successful: number;
  failed: number;
  results: EmailResult[];
}

export interface SendProgressProps {
  sendState: EmailState;
  progress: number;
  processed: number;
  total: number;
  logEntries: EmailResult[];
  summary?: SummaryData | null;
  onReset: () => void;
}

export enum EmailState {
  IDLE,
  SENDING,
  DONE,
}

export interface StatCardProps {
  label: string;
  value: number;
  color: 'primary' | 'success' | 'error';
  icon: React.ReactNode;
}

export interface Attachment {
  fileName: string;
  base64Content: string;
  contentType: string;
  isInline: boolean;
  contentId: string;
}

export interface SendPayload {
  subject: string;
  htmlBody: string;
  attachments: Attachment[];
}

export interface EmailEditorProps {
  onSend: (payload: SendPayload) => void;
  isSending: boolean;
  recipientCount: number;
  subject: string;
  onSubjectChange: (subject: string) => void;
  attachments: Attachment[];
  onAttachmentsChange: (attachments: Attachment[]) => void;
  initialContent?: string;
  onContentChange?: (html: string) => void;
}

export interface Link {
  id: UUIDTypes | null;
  link: string;
  name: string;
  icon: string;
}

export interface SidebarSettings {
  showMail: boolean;
  showSepa: boolean;
  links: Array<Link>;
}

export interface TestUser {
  id: string;
  username: string;
  password: string;
  role: Role;
}

export interface TestContext {
  user: TestUser;
  token: string;
  originalSidebar: SidebarSettings;
}

export const SNACKBAR_INITIAL_STATE: SnackbarState = { status: null, message: '' };

export interface Contribution {
  id: UUIDTypes;
  name: string;
  amount: number;
  dueDate: Date;
  paid: boolean;
  memberNumber: number;
  membershipStatus: MembershipStatus;
  deletedAt: Date | null;
}

export interface ContributionInfo {
  openPayments: number;
  openAmount: number;
}

export interface AuditLog {
  timestamp: string;
  userName: string | null;
  action: string;
  entity: string;
  data: string;
}

export enum CalendarEventVisibility {
  CLUB = 'CLUB',
  PARTICIPANTS = 'PARTICIPANTS',
  PRIVATE = 'PRIVATE',
}

export enum CalendarParticipantRole {
  EDITOR = 'EDITOR',
  ATTENDEE = 'ATTENDEE',
}

export enum CalendarRsvpStatus {
  PENDING = 'PENDING',
  ACCEPTED = 'ACCEPTED',
  TENTATIVE = 'TENTATIVE',
  DECLINED = 'DECLINED',
}

export interface CalendarCategory {
  id: string;
  name: string;
  color: string;
  isPublic: boolean;
}

export interface CalendarUser {
  id: string;
  username: string;
}

export interface CalendarEventOccurrence {
  eventId: string;
  title: string;
  start: string;
  end: string;
  allDay: boolean;
  locationName: string;
  categoryId: string | null;
  color: string | null;
  visibility: CalendarEventVisibility;
  canEdit: boolean;
  isPublic: boolean;
  isRecurring: boolean;
  reminderMinutesBefore: number | null;
  myRsvp: CalendarRsvpStatus | null;
}

export interface CalendarEventParticipant {
  userId: string;
  username: string;
  role: CalendarParticipantRole;
  rsvpStatus: CalendarRsvpStatus;
}

export interface CalendarEvent {
  id: string;
  title: string;
  description: string;
  start: string;
  end: string;
  allDay: boolean;
  locationName: string;
  locationAddress: string;
  locationUrl: string;
  visibility: CalendarEventVisibility;
  isPublic: boolean;
  isPubliclyVisible: boolean;
  recurrenceRule: string | null;
  recurrenceExceptions: string[];
  reminderMinutesBefore: number | null;
  category: CalendarCategory | null;
  owner: CalendarUser;
  participants: CalendarEventParticipant[];
  version: number;
  createdAt: string;
  updatedAt: string;
  lastModifiedBy: CalendarUser | null;
  canEdit: boolean;
  canDelete: boolean;
  isSubscribed: boolean;
  myRsvp: CalendarRsvpStatus | null;
}

export interface CalendarEventPayload {
  title: string;
  description: string;
  start: string;
  end: string;
  allDay: boolean;
  locationName: string;
  locationAddress: string;
  locationUrl: string;
  visibility: CalendarEventVisibility;
  categoryId: string | null;
  isPublic: boolean;
  recurrenceRule: string | null;
  reminderMinutesBefore: number | null;
  participants: { userId: string; role: CalendarParticipantRole }[];
  version?: number;
}

export enum NotificationType {
  CALENDAR_INVITATION = 'CALENDAR_INVITATION',
  CALENDAR_EVENT_CHANGED = 'CALENDAR_EVENT_CHANGED',
  CALENDAR_EVENT_DELETED = 'CALENDAR_EVENT_DELETED',
  CALENDAR_RSVP = 'CALENDAR_RSVP',
  CALENDAR_REMINDER = 'CALENDAR_REMINDER',
}

export interface AppNotification {
  id: string;
  type: NotificationType;
  eventId: string | null;
  eventTitle: string;
  eventStart: string | null;
  actorUsername: string;
  details: string;
  createdAt: string;
  read: boolean;
}

export interface CalendarSettings {
  publicCalendarEnabled: boolean;
  publicCalendarTitle: string;
  requireKey: boolean;
  publicCalendarKey: string | null;
}

export interface PublicCalendarOccurrence {
  eventId: string;
  title: string;
  description: string;
  start: string;
  end: string;
  allDay: boolean;
  locationName: string;
  locationAddress: string;
  locationUrl: string;
  categoryId: string | null;
  categoryName: string | null;
  color: string | null;
}
