import { request } from '@playwright/test';
import { Interval, Link, Member, Role, SidebarSettings, TestUser } from '../src/types';

export const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:80/api';
export const APP_BASE = process.env.APP_BASE_URL ?? 'http://localhost:80';
const ADMIN_USER = process.env.TEST_ADMIN_USER ?? 'admin';
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

  async createUser(role: Role): Promise<TestUser> {
    const ctx = await this.ctx();
    const username = `playwright_${role.toLowerCase()}_${Date.now()}`;
    const password = 'Test123456!';

    const res = await ctx.post('/api/users', {
      data: { username, password, role },
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

  async createMember(member: any): Promise<any> {
    const ctx = await this.ctx();

    const res = await ctx.post('/api/members', {
      data: member,
    });
    if (!res.ok()) {
      throw new Error(`Member creation failed: ${res.status()} ${await res.text()}`);
    }
    await ctx.dispose();
  }

  async createMemberAsUser(member: any, userToken: string): Promise<any> {
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

  async deleteAllMember(): Promise<any> {
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

  async deleteAllContributionPlans(): Promise<any> {
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

  async createContributionPlan(): Promise<any> {
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

  async createTestMemberCategory(): Promise<any> {
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

  async deleteTestMemberCategory(): Promise<any> {
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

  async updateMailSettings(): Promise<any> {
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

  async deleteMailSettings(): Promise<any> {
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
    let pageName = 'Test web page';
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

  async deleteWebPageSettings(): Promise<any> {
    const ctx = await this.ctx();

    const res = await ctx.get('/api/web-page-config');
    if (res.ok()) {
      const result = await res.json();
      await ctx.delete('/api/web-page-config/' + result.id);
    }
    await ctx.dispose();
  }

  async updateLinkSettings(): Promise<any> {
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

  async deleteLinkSettings(): Promise<any> {
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

  async createLinkSettings(link: Link): Promise<any> {
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
}
