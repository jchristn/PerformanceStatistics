// API client for the TaskManager server. The session token lives in sessionStorage (cleared when the tab closes).

const TOKEN_KEY = 'taskmgr.session';

export function getSession() {
  try {
    const raw = sessionStorage.getItem(TOKEN_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function setSession(session) {
  try {
    if (session) sessionStorage.setItem(TOKEN_KEY, JSON.stringify(session));
    else sessionStorage.removeItem(TOKEN_KEY);
  } catch {
    // Storage unavailable; the session lasts for this page only.
  }
  window.dispatchEvent(new Event('taskmgr-session'));
}

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

async function request(method, path, body) {
  const session = getSession();
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (session?.token) headers.Authorization = 'Bearer ' + session.token;
  const resp = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  if (resp.status === 401 && session) setSession(null);
  const text = await resp.text();
  const data = text ? JSON.parse(text) : null;
  if (!resp.ok) throw new ApiError(resp.status, data?.error || `HTTP ${resp.status}`);
  return data;
}

const enc = encodeURIComponent;

function range(from, to, step) {
  const q = new URLSearchParams();
  if (from) q.set('from', from.toISOString());
  if (to) q.set('to', to.toISOString());
  if (step) q.set('step', String(step));
  return q;
}

export const api = {
  async login(username, password) {
    const session = await request('POST', '/api/v1/auth/login', { username, password });
    setSession(session);
    return session;
  },
  async logout() {
    try { await request('POST', '/api/v1/auth/logout'); } catch { /* ignore */ }
    setSession(null);
  },
  catalog: () => request('GET', '/api/v1/catalog'),
  hosts: () => request('GET', '/api/v1/hosts'),
  host: (id) => request('GET', `/api/v1/hosts/${enc(id)}`),
  snapshot: (id) => request('GET', `/api/v1/hosts/${enc(id)}/snapshot`),
  history(id, metric, from, to, step) {
    const q = range(from, to, step);
    q.set('metric', metric);
    return request('GET', `/api/v1/hosts/${enc(id)}/history?${q}`);
  },
  processHistory(id, key, metric, from, to, step) {
    const q = range(from, to, step);
    q.set('metric', metric);
    return request('GET', `/api/v1/hosts/${enc(id)}/processes/${enc(key)}/history?${q}`);
  },
  interfaceHistory(id, name, metric, from, to, step) {
    const q = range(from, to, step);
    q.set('metric', metric);
    return request('GET', `/api/v1/hosts/${enc(id)}/interfaces/${enc(name)}/history?${q}`);
  },
  processRecords(id, from, to) {
    return request('GET', `/api/v1/hosts/${enc(id)}/process-records?${range(from, to)}`);
  },
  accounting(id, from, to, groupBy) {
    const q = range(from, to);
    q.set('groupBy', groupBy);
    return request('GET', `/api/v1/hosts/${enc(id)}/accounting?${q}`);
  },
  requestAction: (id, req) => request('POST', `/api/v1/hosts/${enc(id)}/actions`, req),
  action: (id, requestId) => request('GET', `/api/v1/hosts/${enc(id)}/actions/${enc(requestId)}`),
  audit: (id) => request('GET', `/api/v1/hosts/${enc(id)}/audit?limit=200`),
};

// Live snapshots over WebSocket with automatic reconnect. Returns a function that closes the stream.
export function streamHost(hostId, onSnapshot, onStatus) {
  let socket = null;
  let closed = false;
  let retry = null;

  const connect = () => {
    const session = getSession();
    if (!session) return;
    const proto = location.protocol === 'https:' ? 'wss' : 'ws';
    socket = new WebSocket(`${proto}://${location.host}/api/v1/stream?host=${enc(hostId)}&access_token=${enc(session.token)}`);
    socket.onopen = () => onStatus?.('live');
    socket.onmessage = (e) => {
      try { onSnapshot(JSON.parse(e.data)); } catch { /* ignore malformed */ }
    };
    socket.onclose = () => {
      onStatus?.('reconnecting');
      if (!closed) retry = setTimeout(connect, 2000);
    };
    socket.onerror = () => socket?.close();
  };

  connect();
  return () => {
    closed = true;
    clearTimeout(retry);
    socket?.close();
  };
}
