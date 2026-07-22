import { UUIDTypes } from 'uuid';
import { AlertColor } from '@mui/material/Alert';

export interface ConfigContextType {
  config: { pageName: string; logo: string };
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
  memberCategoryId: UUIDTypes | null;
  iban: string;
  bic: string;
  sepaConsent: Date | null;
  entryDate: Date | null;
  exitDate: Date | null;
  contributionPlanId: UUIDTypes | null;
  deletedAt: Date | null;
}

export interface MemberErrors {
  birthday?: string;
  city?: string;
  contributionPlanId?: string;
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

export enum Interval {
  YEARLY = 'YEARLY',
  MONTHLY = 'MONTHLY',
}

export enum Role {
  ADMIN = 'ADMIN',
  USER = 'USER',
  FINANCIAL_MANAGER = 'FINANCIAL_MANAGER',
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
