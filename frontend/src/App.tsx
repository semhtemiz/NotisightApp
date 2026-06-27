import React, { Suspense, lazy, useEffect, useState } from 'react';
import type { ViewState } from './types';
import { writeStoredUser } from './utils/currentUser';

const AuthScreen = lazy(() => import('./components/AuthScreen').then(module => ({ default: module.AuthScreen })));
const Dashboard = lazy(() => import('./components/Dashboard').then(module => ({ default: module.Dashboard })));
const SettingsPanel = lazy(() => import('./components/SettingsPanel').then(module => ({ default: module.SettingsPanel })));

const AppFallback = () => (
  <div className="flex h-full w-full items-center justify-center bg-ns-bg-primary text-sm text-ns-text-muted">
    Yükleniyor...
  </div>
);

export default function App() {
  const [view, setView] = useState<ViewState>(() => {
    return localStorage.getItem('accessToken') ? 'app' : 'auth';
  });

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    writeStoredUser(null);
    setView('auth');
  };

  useEffect(() => {
    const handleUnauthorized = () => setView('auth');
    window.addEventListener('auth:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', handleUnauthorized);
  }, []);

  useEffect(() => {
    const savedTheme = localStorage.getItem('theme') || 'Koyu';

    if (savedTheme === 'Açık') {
      document.documentElement.classList.add('light');
    } else if (savedTheme === 'Sistem') {
      if (window.matchMedia('(prefers-color-scheme: light)').matches) {
        document.documentElement.classList.add('light');
      } else {
        document.documentElement.classList.remove('light');
      }
    } else {
      document.documentElement.classList.remove('light');
    }
  }, []);

  return (
    <div className="h-screen w-screen overflow-hidden bg-ns-bg-primary font-sans antialiased text-ns-text-primary">
      <Suspense fallback={<AppFallback />}>
        {view === 'auth' && (
          <AuthScreen onLogin={() => setView('app')} />
        )}
        {view === 'app' && (
          <Dashboard onOpenSettings={() => setView('settings')} />
        )}
        {view === 'settings' && (
          <SettingsPanel onBack={() => setView('app')} onLogout={handleLogout} />
        )}
      </Suspense>
    </div>
  );
}
