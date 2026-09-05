import { request } from '@playwright/test';
import {
  BulkMail,
  Gender,
  Interval,
  Link,
  MemberApiResult,
  MemberPayload,
  ReceiptApiResult,
  ReceiptCategoryApiResult,
  Role,
  SidebarSettings,
  TaskWithinTheClub,
  TestUser,
} from '../src/types';

export const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:80/api';
export const APP_BASE = process.env.APP_BASE_URL ?? 'http://localhost:80';
const ADMIN_USER = process.env.TEST_ADMIN_USER ?? 'Admin';
const ADMIN_PASS = process.env.TEST_ADMIN_PASS ?? 'admin123';

export class BackendClient {
  private adminToken: string | null = null;

  defaultMemberCategories = {
    student: 'bbd21be1-4d05-437f-ae76-f65b66290438',
    alumni: '67853de0-3d93-45ad-8aa4-a356441cdab9',
    other: 'd0dd905b-a088-4dca-9b3a-4640ab66fadd',
    all: '73a9b489-f31d-4517-8481-a040c5c13bde',
    boardOfDirectors: '7da5c063-439b-4895-9e01-ee6e9e31d569',
  };

  async init() {
    const ctx = await request.newContext({ baseURL: API_BASE });
    const res = await ctx.post('/api/auth/login', {
      data: { username: ADMIN_USER, password: ADMIN_PASS },
    });
    if (!res.ok()) {
      throw new Error(`Admin-Login failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    this.adminToken = body.token ?? body.accessToken ?? body.jwt;
    await ctx.dispose();
  }

  private async ctx() {
    return await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: {
        Authorization: `Bearer ${this.adminToken}`,
        'Content-Type': 'application/json',
      },
    });
  }

  private async userCtx(userToken: string) {
    return await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: {
        Authorization: `Bearer ${userToken}`,
        'Content-Type': 'application/json',
      },
    });
  }

  async createUser(role: Role, email?: string): Promise<TestUser> {
    const ctx = await this.ctx();
    const username = `playwright_${role.toLowerCase()}_${Date.now()}`;
    const password = 'Test123456!';

    const res = await ctx.post('/api/users', {
      data: { username, password, role, email },
    });
    if (!res.ok()) {
      throw new Error(`User creation failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    await ctx.dispose();

    return { id: body.id, username, password, role };
  }

  async deleteUser(userId: string) {
    const ctx = await this.ctx();
    await ctx.delete(`/api/users/${userId}`);
    await ctx.dispose();
  }

  async createMember(member: MemberPayload): Promise<MemberApiResult> {
    const ctx = await this.ctx();

    const res = await ctx.post('/api/members', {
      data: member,
    });
    if (!res.ok()) {
      throw new Error(`Member creation failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    await ctx.dispose();
    return body;
  }

  async createTestMember(overrides: Partial<MemberPayload> = {}): Promise<MemberApiResult> {
    return this.createMember({
      academicDegree: null,
      birthday: '2026-03-14T23:00:00.000Z',
      city: 'Kiel',
      countryCode: 'DE',
      contributionPlanId: null,
      courseOfStudy: '',
      email: 'test_1774694157026@test.de',
      bulkMail: BulkMail.ALLOWED,
      endOfStudies: '2026-02-28T23:00:00.000Z',
      entryDate: '2025-03-20T10:35:53.998Z',
      exitDate: '2026-03-19T23:00:00.000Z',
      firstName: 'Test',
      gender: Gender.DIVERSE,
      iban: 'DE40998929246819178888',
      bic: 'DEUTDEDEXXX',
      id: '00000000-0000-0000-0000-000000000000',
      lastName: 'Tester',
      memberCategoryId: this.defaultMemberCategories.other,
      memberNumber: 1,
      middleName: '',
      phone: '',
      postalCode: '24103',
      sepaConsent: '2026-03-15T23:00:00.000Z',
      startOfStudies: '2025-02-28T23:00:00.000Z',
      street: 'Teststr. 3',
      taskWithinTheClub: TaskWithinTheClub.MEMBER,
      ...overrides,
    });
  }

  async deleteMember(memberId: string): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.delete('/api/members/' + memberId);
    if (!res.ok()) {
      throw new Error(`Member deletion failed: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async createMemberAsUser(member: MemberPayload, userToken: string): Promise<void> {
    const ctx = await this.userCtx(userToken);

    const res = await ctx.post('/api/members', {
      data: member,
    });
    if (!res.ok()) {
      throw new Error(`Failed to create member: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async softDeleteMember(userToken: string) {
    const ctx = await this.userCtx(userToken);
    const res = await ctx.get('/api/members');
    if (res.ok()) {
      const result = await res.json();
      for (const member of result.items) {
        await ctx.delete('/api/members/' + member.id);
      }
    }
    await ctx.dispose();
  }

  async deleteAllMember(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/members?limit=300');
    if (res.ok()) {
      const result = await res.json();
      for (const member of result.items) {
        await ctx.delete('/api/members/' + member.id);
      }
    }
    await ctx.dispose();
  }

  async deleteAllContributionPlans(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/contribution-plans');
    if (res.ok()) {
      const result = await res.json();
      for (const contributionPlan of result.items) {
        if (contributionPlan.name != 'Default') {
          await ctx.delete('/api/contribution-plans/' + contributionPlan.id);
        }
      }
    }
    await ctx.dispose();
  }

  async createContributionPlan(): Promise<string> {
    const ctx = await this.ctx();
    const contributionPlan = `playwright_${Date.now()}`;

    const res = await ctx.post('/api/contribution-plans', {
      data: {
        name: contributionPlan,
        amount: 8000,
        interval: Interval.MONTHLY,
      },
    });
    if (!res.ok()) {
      throw new Error(`Failed to create contribution plan: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();

    return contributionPlan;
  }

  async createTestMemberCategory(): Promise<string> {
    const ctx = await this.ctx();
    const memberCategory = `test`;

    const res = await ctx.post('/api/member-categories', {
      data: {
        name: memberCategory,
        category: 'TEST',
      },
    });
    if (!res.ok()) {
      throw new Error(`Failed to create member category: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();

    return memberCategory;
  }

  async deleteTestMemberCategory(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/member-categories');
    if (res.ok()) {
      const result = await res.json();
      for (const contributionPlan of result.items) {
        if (contributionPlan.name === 'test' || contributionPlan.name === 'main') {
          await ctx.delete('/api/member-categories/' + contributionPlan.id);
        }
      }
    }
    await ctx.dispose();
  }

  async updateMailSettings(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.put('/api/mail', {
      data: {
        smtpServer: 'papercut',
        port: 2525,
        imapServer: 'papercut',
        imapPort: 2525,
        username: 'test',
        password: 'test',
        fromMail: 'noreply@test.de',
        enableSsl: false,
      },
    });
    if (!res.ok()) {
      throw new Error(`Failed to create mail settings: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async deleteMailSettings(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/mail');
    if (res.ok()) {
      const result = await res.json();
      await ctx.delete('/api/mail/' + result.id);
    }
    await ctx.dispose();
  }

  async updateWebPageSettings(): Promise<string> {
    const ctx = await this.ctx();
    const pageName = 'Test web page';
    const res = await ctx.put('/api/web-page-config', {
      data: {
        pageName,
      },
    });
    if (!res.ok()) {
      throw new Error(`Failed to create webpage settings: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
    return pageName;
  }

  async deleteWebPageSettings(): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/web-page-config');
    if (res.ok()) {
      const result = await res.json();
      await ctx.delete('/api/web-page-config/' + result.id);
    }
    await ctx.dispose();
  }

  async updateLinkSettings(): Promise<void> {
    const ctx = await this.ctx();
    const res = await ctx.post(`/api/link`, {
      data: JSON.stringify({
        link: 'http://localhost:8080',
        name: 'Dateien',
        icon: '',
      }),
    });
    if (!res.ok()) {
      throw new Error(`Failed to create link settings: ${res.status()}`);
    }
    await ctx.dispose();
  }

  async deleteLinkSettings(): Promise<void> {
    const ctx = await this.ctx();

    let res = await ctx.get('/api/link');
    if (res.ok()) {
      const response = await res.json();

      for (const item of response.items) {
        res = await ctx.delete('/api/link/' + item.id);
        if (!res.ok()) {
          throw new Error(`Failed to deleted link settings: ${res.status()}`);
        }
      }
    }

    await ctx.dispose();
  }

  async createLinkSettings(link: Link): Promise<void> {
    const ctx = await this.ctx();

    const res = await ctx.post(`/api/link`, {
      data: JSON.stringify({
        link: link.link,
        name: link.name,
        icon: link.icon,
      }),
    });

    if (!res.ok()) {
      throw new Error(`Failed to create link settings: ${res.status()}`);
    }

    await ctx.dispose();
  }

  async setSelfEnrollmentEnabled(enabled: boolean): Promise<void> {
    const ctx = await this.ctx();
    let pageName = '';
    let logo = '';
    const existing = await ctx.get('/api/web-page-config');
    if (existing.ok()) {
      const current = await existing.json();
      pageName = current.pageName;
      logo = current.logo;
    }

    const res = await ctx.put('/api/web-page-config', {
      data: { pageName, logo, selfEnrollmentEnabled: enabled },
    });
    if (!res.ok()) {
      throw new Error(`Failed to set self-enrollment config: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async loginUser(username: string, password: string): Promise<string> {
    const ctx = await request.newContext({ baseURL: API_BASE });
    const res = await ctx.post('/api/auth/login', {
      data: { username, password },
    });
    if (!res.ok()) {
      throw new Error(`Login failed for ${username}: ${res.status()}`);
    }
    const body = await res.json();
    await ctx.dispose();
    return body.token ?? body.accessToken ?? body.jwt;
  }

  async setSidebarSettings(settings: SidebarSettings) {
    const ctx = await this.ctx();

    if (settings.showMail) {
      const res = await ctx.put('/api/mail', {
        data: JSON.stringify({
          smtpServer: 'test',
          port: 587,
          imapServer: 'test',
          imapPort: 587,
          username: 'test',
          password: 'test',
          fromMail: 'test@test.de',
        }),
      });
      if (!res.ok()) {
        throw new Error(`Failed to set mail settings: ${res.status()}`);
      }
    } else {
      let res = await ctx.get('/api/mail');
      if (res.ok()) {
        res = await ctx.delete('/api/mail/' + (await res.json()).id);
        if (!res.ok()) {
          throw new Error(`Failed to set mail settings: ${res.status()}`);
        }
      }
    }

    if (settings.showSepa) {
      const res = await ctx.put('/api/creditor-config', {
        data: JSON.stringify({
          name: 'test',
          iban: 'iban',
          bic: 'bic',
          creditorId: 'test',
          streetNameAndNumber: 'test',
          postCode: '24103',
          cityName: 'test',
          countryCode: 'DE',
        }),
      });
      if (!res.ok()) {
        throw new Error(`Failed to set creditor settings: ${res.status()}`);
      }
    } else {
      let res = await ctx.get('/api/creditor-config');
      if (res.ok()) {
        res = await ctx.delete('/api/creditor-config/' + (await res.json()).id);
        if (!res.ok()) {
          throw new Error(`Failed to set creditor settings: ${res.status()}`);
        }
      }
    }

    if (settings.links.length > 0) {
      await this.createLinkSettings({
        id: null,
        link: settings.links[0].link,
        name: settings.links[0].name,
        icon: settings.links[0].icon,
      });
    } else {
      await this.deleteLinkSettings();
    }

    await ctx.dispose();
  }

  async getSidebarSettings(): Promise<SidebarSettings> {
    const ctx = await this.ctx();
    const res = await ctx.get('/api/web-page-config/sidebar');
    const body = await res.json();
    await ctx.dispose();
    return body;
  }

  async createReceiptCategory(
    name = 'playwright_test_category',
  ): Promise<ReceiptCategoryApiResult> {
    const ctx = await this.ctx();
    const res = await ctx.post('/api/receipt-categories', { data: { name } });
    if (!res.ok()) {
      throw new Error(`Receipt category creation failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    await ctx.dispose();
    return body;
  }

  async deleteAllReceiptCategories(): Promise<void> {
    const ctx = await this.ctx();
    const res = await ctx.get('/api/receipt-categories');
    if (res.ok()) {
      const result = await res.json();
      for (const category of result.items) {
        await ctx.delete(`/api/receipt-categories/${category.id}`);
      }
    }
    await ctx.dispose();
  }

  async createTestReceipt(
    token: string,
    overrides: Record<string, string> = {},
  ): Promise<ReceiptApiResult> {
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const fields = {
      amount: '19.99',
      receiptDate: '2026-01-15T00:00:00.000Z',
      vendor: 'Playwright Testhaendler',
      ...overrides,
    };

    const res = await ctx.post('/api/receipts', { multipart: fields });
    if (!res.ok()) {
      throw new Error(`Receipt creation failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    await ctx.dispose();
    return body;
  }

  async createTestReceiptWithPdf(
    token: string,
    pdfBytes: Buffer,
    overrides: Record<string, string> = {},
  ): Promise<ReceiptApiResult> {
    const ctx = await request.newContext({
      baseURL: API_BASE,
      extraHTTPHeaders: { Authorization: `Bearer ${token}` },
    });
    const fields = {
      amount: '19.99',
      receiptDate: '2026-01-15T00:00:00.000Z',
      vendor: 'Playwright Testhaendler',
      ...overrides,
    };

    const res = await ctx.post('/api/receipts', {
      multipart: {
        ...fields,
        files: { name: 'receipt.pdf', mimeType: 'application/pdf', buffer: pdfBytes },
      },
    });
    if (!res.ok()) {
      throw new Error(`Receipt creation failed: ${res.status()} ${await res.text()}`);
    }
    const body = await res.json();
    await ctx.dispose();
    return body;
  }

  async getReceiptFile(
    token: string,
    receiptId: string,
    fileId: string,
  ): Promise<{ status: number; contentType: string | undefined; body: Buffer }> {
    const ctx = await this.userCtx(token);
    const res = await ctx.get(`/api/receipts/${receiptId}/files/${fileId}`);
    const status = res.status();
    const contentType = res.headers()['content-type'];
    const body = await res.body();
    await ctx.dispose();
    return { status, contentType, body };
  }

  async deleteAllReceipts(): Promise<void> {
    const ctx = await this.ctx();
    const res = await ctx.get('/api/receipts?limit=300');
    if (res.ok()) {
      const result = await res.json();
      for (const receipt of result.items) {
        await ctx.delete('/api/receipts/' + receipt.id + '/hard');
      }
    }
    await ctx.dispose();
  }

  async setUserSetting(token: string, type: string, enabled: boolean): Promise<void> {
    const ctx = await this.userCtx(token);
    const res = await ctx.put(`/api/users/account/settings/${type}`, { data: { enabled } });
    if (!res.ok()) {
      throw new Error(`Failed to set user setting: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async payReceipt(token: string, receiptId: string, paymentMethod?: string): Promise<void> {
    const ctx = await this.userCtx(token);
    const res = await ctx.post(`/api/receipts/${receiptId}/pay`, {
      data: { paymentMethod: paymentMethod ?? null },
    });
    if (!res.ok()) {
      throw new Error(`Marking receipt as paid failed: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }
}
