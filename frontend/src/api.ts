const API = '/api';

const CSRF_HEADERS = { 'X-UV-CSRF': '1' };

export type Session = {
  id: string;
  name: string;
  role: string;
};

export async function apiFile(path: string, options: RequestInit = {}) {
  return await fetch(`${API}${path}`, {
    ...options,
    credentials: 'same-origin',
    headers: {
      ...CSRF_HEADERS,
      ...(options.headers || {}),
    },
  });
}

export async function api(path: string, options: RequestInit = {}) {
  const res = await fetch(`${API}${path}`, {
    ...options,
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      ...CSRF_HEADERS,
      ...(options.headers || {}),
    },
  });

  if (!res.ok && res.status === 400) throw new Error('Bad Request');

  if (!res.ok && res.status !== 409 && res.status !== 429) throw new Error('API Error');
  if (!res.ok) return res.status;

  if (res.status === 204) return;

  if (res.status === 207) {
    return { status: 207, message: (await res.json()).message };
  }

  const contentType = res.headers.get('content-type');
  if (!contentType || !contentType.includes('application/json')) {
    return null;
  }

  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export async function fetchSession(): Promise<Session | null> {
  const res = await fetch(`${API}/auth/me`, { credentials: 'same-origin' });
  if (res.status === 401) return null;
  if (!res.ok) throw new Error('Session check failed');
  return await res.json();
}

export async function logout() {
  await fetch(`${API}/auth/logout`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: CSRF_HEADERS,
  });
}

export async function login(username: string, password: string) {
  return await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });
}

export async function loginTwoFactor(challenge: string, code: string) {
  return await fetch(`${API}/auth/login/2fa`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challenge, code }),
  });
}
