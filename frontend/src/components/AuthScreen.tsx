import React, { useState } from 'react';
import { motion } from 'motion/react';
import { AlertCircle, Check, Eye, EyeOff, Loader2, Lock, Mail, User, X } from 'lucide-react';
import { apiClient } from '../utils/apiClient';
import { writeStoredUser } from '../utils/currentUser';
import logoUrl from '../assets/logo.svg';

interface AuthScreenProps {
  onLogin: () => void;
}

export const AuthScreen: React.FC<AuthScreenProps> = ({ onLogin }) => {
  const [isLoginMode, setIsLoginMode] = useState(true);
  const [username, setUsername] = useState('');
  const [identifier, setIdentifier] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showPasswordRules, setShowPasswordRules] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const passwordRules = [
    { label: 'En az 8 karakter', isValid: password.length >= 8 },
    { label: 'Büyük harf içerir', isValid: /[A-ZÇĞİÖŞÜ]/.test(password) },
    { label: 'Küçük harf içerir', isValid: /[a-zçğıöşü]/.test(password) },
    { label: 'Sayı içerir', isValid: /\d/.test(password) }
  ];

  const isPasswordValid = passwordRules.every(rule => rule.isValid);
  const passwordsMatch = password.length > 0 && confirmPassword.length > 0 && password === confirmPassword;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const normalizedEmail = email.trim();
      const normalizedUsername = username.trim();
      const normalizedIdentifier = identifier.trim();

      if (isLoginMode) {
        const data = await apiClient.post('/auth/login', { identifier: normalizedIdentifier, password });
        localStorage.setItem('accessToken', data.accessToken);
        localStorage.setItem('refreshToken', data.refreshToken);
        writeStoredUser(data.user);
        onLogin();
      } else {
        if (!isPasswordValid) {
          setError('Şifreniz tüm güvenlik şartlarını karşılamalı.');
          return;
        }

        if (!/^[a-zA-Z0-9._-]{3,60}$/.test(normalizedUsername)) {
          setError('Kullanıcı adı 3-60 karakter olmalı; yalnızca harf, sayı, nokta, alt çizgi ve kısa çizgi içerebilir.');
          return;
        }

        if (password !== confirmPassword) {
          setError('Şifreler eşleşmiyor.');
          return;
        }

        const data = await apiClient.post('/auth/register', {
          username: normalizedUsername,
          email: normalizedEmail,
          password
        });
        localStorage.setItem('accessToken', data.accessToken);
        localStorage.setItem('refreshToken', data.refreshToken);
        writeStoredUser(data.user);
        onLogin();
      }
    } catch (err: any) {
      setError(err?.message || 'İşlem başarısız. Lütfen bilgilerinizi kontrol edin.');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const switchMode = (nextLoginMode = !isLoginMode) => {
    setIsLoginMode(nextLoginMode);
    setError('');
    setShowPassword(false);
    setShowPasswordRules(false);
    setConfirmPassword('');
  };

  return (
    <div className="relative flex h-full w-full items-center justify-center overflow-hidden bg-ns-bg-primary px-4">
      <div className="pointer-events-none absolute inset-x-[18%] top-[14%] h-64 rounded-full bg-ns-primary/10 blur-3xl" />
      <div className="pointer-events-none absolute bottom-[14%] right-[20%] h-48 w-48 rounded-full bg-ns-green-glow/10 blur-3xl" />

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="ns-glass relative z-10 w-full max-w-[440px] overflow-hidden rounded-3xl shadow-2xl shadow-black/25"
      >
        <div className="p-6 sm:p-7">
          <div className="mb-6 flex flex-col items-center text-center">
            <div className="mb-4 flex rounded-2xl border border-ns-primary/20 bg-ns-primary/10 p-3 shadow-lg shadow-ns-primary/10">
              <img src={logoUrl} alt="Notisight" className="h-8 w-8 object-contain" />
            </div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-[0.22em] text-ns-primary/90">Notisight</p>
            <h2 className="text-2xl font-semibold tracking-normal text-ns-text-primary">
              {isLoginMode ? 'Tekrar hoş geldiniz' : 'Çalışma alanınızı başlatın'}
            </h2>
            <p className="mt-2 max-w-sm text-sm leading-6 text-ns-text-muted">
              {isLoginMode
                ? 'Notlarınıza, kaynaklarınıza ve AI çalışma alanınıza devam edin.'
                : 'Kişisel bilgi alanınız için kullanıcı adınızı ve güvenli şifrenizi belirleyin.'}
            </p>
          </div>

          <div className="mb-5 grid grid-cols-2 rounded-2xl border border-ns-border/55 bg-ns-bg-primary/45 p-1 shadow-inner shadow-black/10">
            {[
              { label: 'Giriş Yap', loginMode: true },
              { label: 'Kayıt Ol', loginMode: false }
            ].map(tab => (
              <button
                key={tab.label}
                type="button"
                onClick={() => switchMode(tab.loginMode)}
                disabled={isLoading}
                className={`h-10 rounded-xl text-sm font-semibold transition-all ${
                  isLoginMode === tab.loginMode
                    ? 'bg-ns-primary text-white shadow-sm shadow-ns-primary/20'
                    : 'text-ns-text-secondary hover:bg-ns-surface-hover/65 hover:text-ns-text-primary'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {error && (
            <div className="mb-4 flex items-start gap-2 rounded-2xl border border-red-500/25 bg-red-500/10 p-3 text-sm leading-5 text-red-300">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form className="space-y-4" onSubmit={handleSubmit}>
            {!isLoginMode && (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-secondary">Kullanıcı adı</label>
                <div className="relative">
                  <User className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ns-text-muted" />
                  <input
                    type="text"
                    placeholder="semih"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    className="h-11 w-full rounded-2xl border border-ns-border/75 bg-ns-bg-primary/72 pl-10 pr-4 text-sm text-ns-text-primary shadow-inner shadow-black/5 outline-none transition-all placeholder:text-ns-text-disabled focus:border-ns-primary focus:ring-2 focus:ring-ns-primary/20 disabled:cursor-not-allowed disabled:opacity-60"
                    autoComplete="username"
                    disabled={isLoading}
                    required={!isLoginMode}
                    minLength={3}
                    maxLength={60}
                    pattern="[a-zA-Z0-9._-]+"
                  />
                </div>
                <p className="text-xs text-ns-text-muted">
                  Harf, sayı, nokta, alt çizgi ve kısa çizgi kullanabilirsiniz.
                </p>
              </div>
            )}

            {isLoginMode ? (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-secondary">E-posta veya kullanıcı adı</label>
                <div className="relative">
                  <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ns-text-muted" />
                  <input
                    type="text"
                    placeholder="isim@ornek.com veya semih"
                    value={identifier}
                    onChange={(e) => setIdentifier(e.target.value)}
                    className="h-11 w-full rounded-2xl border border-ns-border/75 bg-ns-bg-primary/72 pl-10 pr-4 text-sm text-ns-text-primary shadow-inner shadow-black/5 outline-none transition-all placeholder:text-ns-text-disabled focus:border-ns-primary focus:ring-2 focus:ring-ns-primary/20 disabled:cursor-not-allowed disabled:opacity-60"
                    autoComplete="username"
                    disabled={isLoading}
                    required
                  />
                </div>
              </div>
            ) : (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-secondary">E-posta adresi</label>
                <div className="relative">
                  <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ns-text-muted" />
                  <input
                    type="email"
                    placeholder="isim@ornek.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="h-11 w-full rounded-2xl border border-ns-border/75 bg-ns-bg-primary/72 pl-10 pr-4 text-sm text-ns-text-primary shadow-inner shadow-black/5 outline-none transition-all placeholder:text-ns-text-disabled focus:border-ns-primary focus:ring-2 focus:ring-ns-primary/20 disabled:cursor-not-allowed disabled:opacity-60"
                    autoComplete="email"
                    disabled={isLoading}
                    required
                  />
                </div>
              </div>
            )}
            
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-ns-text-secondary">Şifre</label>
              <div className="relative">
                <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ns-text-muted" />
                <input 
                  type={showPassword ? 'text' : 'password'}
                  placeholder="En az 8 karakter"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onFocus={() => !isLoginMode && setShowPasswordRules(true)}
                  onBlur={() => setShowPasswordRules(false)}
                  className="h-11 w-full rounded-2xl border border-ns-border/75 bg-ns-bg-primary/72 pl-10 pr-11 text-sm text-ns-text-primary shadow-inner shadow-black/5 outline-none transition-all placeholder:text-ns-text-disabled focus:border-ns-primary focus:ring-2 focus:ring-ns-primary/20 disabled:cursor-not-allowed disabled:opacity-60"
                  autoComplete={isLoginMode ? 'current-password' : 'new-password'}
                  disabled={isLoading}
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(prev => !prev)}
                  className="absolute right-2 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-xl text-ns-text-muted transition-colors hover:bg-ns-surface-hover/70 hover:text-ns-text-primary"
                  title={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                  disabled={isLoading}
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>

                {!isLoginMode && showPasswordRules && (
                  <motion.div
                    initial={{ opacity: 0, y: -4 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="ns-glass absolute left-0 right-0 top-[calc(100%+8px)] z-20 rounded-2xl p-3 shadow-xl shadow-black/30"
                  >
                    <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-ns-text-muted">
                      Şifre şartları
                    </div>
                    <div className="space-y-1.5">
                      {passwordRules.map(rule => (
                        <div key={rule.label} className="flex items-center gap-2 text-xs text-ns-text-secondary">
                          <span className={`flex h-4 w-4 items-center justify-center rounded-full border ${
                            rule.isValid
                              ? 'border-ns-primary bg-ns-primary text-white'
                              : 'border-ns-border bg-ns-bg-primary text-ns-text-disabled'
                          }`}>
                            {rule.isValid ? <Check className="h-3 w-3" /> : <X className="h-3 w-3" />}
                          </span>
                          <span className={rule.isValid ? 'text-ns-text-primary' : ''}>{rule.label}</span>
                        </div>
                      ))}
                    </div>
                  </motion.div>
                )}
              </div>
            </div>

            {!isLoginMode && (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-secondary">Şifre tekrar</label>
                <div className="relative">
                  <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ns-text-muted" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="Şifrenizi yeniden girin"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className={`h-11 w-full rounded-2xl border bg-ns-bg-primary/72 pl-10 pr-10 text-sm text-ns-text-primary shadow-inner shadow-black/5 outline-none transition-all placeholder:text-ns-text-disabled focus:ring-2 disabled:cursor-not-allowed disabled:opacity-60 ${
                      confirmPassword.length === 0
                        ? 'border-ns-border focus:border-ns-primary focus:ring-ns-primary/20'
                        : passwordsMatch
                          ? 'border-ns-primary/70 focus:border-ns-primary focus:ring-ns-primary/20'
                          : 'border-red-500/60 focus:border-red-400 focus:ring-red-500/20'
                    }`}
                    autoComplete="new-password"
                    disabled={isLoading}
                    required
                  />
                  {confirmPassword.length > 0 && (
                    <span className={`pointer-events-none absolute right-3 top-1/2 flex h-5 w-5 -translate-y-1/2 items-center justify-center rounded-full ${
                      passwordsMatch ? 'bg-ns-primary text-white' : 'bg-red-500/15 text-red-300'
                    }`}>
                      {passwordsMatch ? <Check className="h-3.5 w-3.5" /> : <X className="h-3.5 w-3.5" />}
                    </span>
                  )}
                </div>
                {confirmPassword.length > 0 && !passwordsMatch && (
                  <p className="text-xs text-red-300">Şifreler eşleşmiyor.</p>
                )}
              </div>
            )}

            <button 
              type="submit"
              disabled={isLoading || (!isLoginMode && (!isPasswordValid || !passwordsMatch))}
              className="mt-2 flex h-11 w-full items-center justify-center gap-2 rounded-2xl bg-ns-primary text-sm font-semibold text-white shadow-lg shadow-ns-primary/20 transition-colors hover:bg-ns-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isLoading && <Loader2 className="h-4 w-4 animate-spin" />}
              {isLoading ? 'Lütfen bekleyin...' : (isLoginMode ? 'Giriş Yap' : 'Kayıt Ol')}
            </button>
          </form>

          <p className="mt-6 text-center text-sm text-ns-text-secondary">
            {isLoginMode ? 'Hesabınız yok mu? ' : 'Zaten hesabınız var mı? '}
            <button 
              onClick={() => switchMode()}
              type="button"
              className="font-semibold text-ns-primary transition-colors hover:text-ns-primary-hover"
              disabled={isLoading}
            >
              {isLoginMode ? 'Kayıt Ol' : 'Giriş Yap'}
            </button>
          </p>
        </div>
      </motion.div>
    </div>
  );
};
