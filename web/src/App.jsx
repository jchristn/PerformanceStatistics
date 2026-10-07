import { useEffect, useState } from 'react';
import { BrowserRouter, Link, Route, Routes } from 'react-router-dom';
import { api, getSession } from './api.js';
import Login from './pages/Login.jsx';
import Hosts from './pages/Hosts.jsx';
import Host from './pages/Host.jsx';

const THEME_KEY = 'taskmgr.theme';

function useTheme() {
  const [theme, setTheme] = useState(() => {
    try { return localStorage.getItem(THEME_KEY) || 'system'; } catch { return 'system'; }
  });
  useEffect(() => {
    if (theme === 'system') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem(THEME_KEY, theme); } catch { /* per-viewer convenience only */ }
  }, [theme]);
  return [theme, setTheme];
}

export default function App() {
  const [session, setSession] = useState(getSession);
  const [theme, setTheme] = useTheme();

  useEffect(() => {
    const onChange = () => setSession(getSession());
    window.addEventListener('taskmgr-session', onChange);
    return () => window.removeEventListener('taskmgr-session', onChange);
  }, []);

  return (
    <BrowserRouter>
      <header className="topbar">
        <Link to="/" className="brand"><img src="/icon.svg" alt="" width="20" height="20" /> TaskManager</Link>
        <div className="topbar-right">
          <select value={theme} onChange={(e) => setTheme(e.target.value)} aria-label="Theme">
            <option value="system">System theme</option>
            <option value="light">Light</option>
            <option value="dark">Dark</option>
          </select>
          {session && (
            <>
              <span className="muted small">{session.username}{session.isAdmin ? ' (admin)' : ''}</span>
              <button type="button" onClick={() => api.logout()}>Sign out</button>
            </>
          )}
        </div>
      </header>
      {!session ? <Login /> : (
        <Routes>
          <Route path="/" element={<Hosts />} />
          <Route path="/hosts/:hostId/*" element={<Host />} />
        </Routes>
      )}
    </BrowserRouter>
  );
}
