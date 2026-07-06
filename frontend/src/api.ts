const API = '/api';

export async function apiFile(path: string, options: any = {}) {
  const token = localStorage.getItem('token');
  return await fetch(`${API}${path}`, {
    ...options,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(options.headers || {}),
    },
  });
}

export async function api(path: string, options: any = {}) {
  const token = localStorage.getItem('token');
  const res = await fetch(`${API}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(options.headers || {}),
    },
  });

  if (!res.ok && res.status === 400) throw new Error('Bad Request');

  if (!res.ok && res.status !== 409) throw new Error('API Error');
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

export function authHeader() {
  const token = localStorage.getItem('token');
  return {
    Authorization: `Bearer ${token}`,
  };
}

export async function login(username: string, password: string) {
  return await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });
}
