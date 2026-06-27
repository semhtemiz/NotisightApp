import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  Key,
  LogOut,
  Monitor,
  Moon,
  Palette,
  Save,
  ShieldCheck,
  Sparkles,
  Sun,
  User
} from 'lucide-react';
import { apiClient } from '../utils/apiClient';
import { writeStoredUser } from '../utils/currentUser';

interface SettingsPanelProps {
  onBack: () => void;
  onLogout: () => void;
}

type SettingsTab = 'Hesabım' | 'Görünüm' | 'API Bağlantıları';

const fieldClass =
  'w-full block bg-ns-bg-secondary/74 border border-ns-border/75 rounded-xl px-4 py-2.5 text-sm text-ns-text-primary placeholder:text-ns-text-disabled shadow-inner shadow-black/5 focus:outline-none focus:ring-2 focus:ring-ns-primary/35 focus:border-ns-primary transition-colors';

const sectionClass = 'ns-glass rounded-2xl p-5 sm:p-6';

export const SettingsPanel: React.FC<SettingsPanelProps> = ({ onBack, onLogout }) => {
  const [activeTab, setActiveTab] = useState<SettingsTab>('Hesabım');
  const [name, setName] = useState('');
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileMessage, setProfileMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [passwordMessage, setPasswordMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [selectedAvatar, setSelectedAvatar] = useState(() => localStorage.getItem('avatarSeed') || 'Felix');
  const [showAvatarGrid, setShowAvatarGrid] = useState(false);
  const [tempTheme, setTempTheme] = useState(() => localStorage.getItem('theme') || 'Koyu');
  const [apiProviders, setApiProviders] = useState<any[]>([]);
  const [apiLoading, setApiLoading] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const avatarSeeds = ['Felix', 'Aneka', 'Jude', 'Oliver', 'Mia', 'Leo', 'Noah', 'Emma'];

  const passwordRules = [
    { label: 'En az 8 karakter', valid: newPassword.length >= 8 },
    { label: 'Büyük harf içerir', valid: /[A-ZÇĞİÖŞÜ]/.test(newPassword) },
    { label: 'Küçük harf içerir', valid: /[a-zçğıöşü]/.test(newPassword) },
    { label: 'Sayı içerir', valid: /\d/.test(newPassword) },
    { label: 'Tekrar şifre ile eşleşir', valid: newPassword.length > 0 && newPassword === confirmPassword },
  ];
  const isPasswordValid = passwordRules.every(rule => rule.valid) && currentPassword.length > 0;

  React.useEffect(() => {
    if (activeTab === 'API Bağlantıları') {
      fetchApiProviders();
    }
  }, [activeTab]);

  React.useEffect(() => {
    fetchCurrentUser();
  }, []);

  const fetchCurrentUser = async () => {
    setProfileLoading(true);
    setProfileMessage(null);
    try {
      const user = await apiClient.get('/auth/me');
      setName(user.displayName || user.username || '');
      setUsername(user.username || '');
      setEmail(user.email || '');
      writeStoredUser(user);
    } catch (err: any) {
      setProfileMessage({ type: 'error', text: err.message || 'Hesap bilgileri yüklenemedi.' });
    } finally {
      setProfileLoading(false);
    }
  };

  const saveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setProfileSaving(true);
    setProfileMessage(null);
    try {
      const user = await apiClient.put('/auth/profile', {
        displayName: name.trim(),
        username: username.trim(),
        email: email.trim()
      });
      setName(user.displayName || user.username || '');
      setUsername(user.username || '');
      setEmail(user.email || '');
      writeStoredUser(user);
      setProfileMessage({ type: 'success', text: 'Profil bilgileri güncellendi.' });
    } catch (err: any) {
      setProfileMessage({ type: 'error', text: err.message || 'Profil bilgileri güncellenemedi.' });
    } finally {
      setProfileSaving(false);
    }
  };

  const changePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordMessage(null);

    if (!isPasswordValid) {
      setPasswordMessage({ type: 'error', text: 'Şifre koşullarını tamamlayın ve mevcut şifrenizi girin.' });
      return;
    }

    setPasswordSaving(true);
    try {
      await apiClient.put('/auth/password', {
        currentPassword,
        newPassword
      });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setPasswordMessage({ type: 'success', text: 'Şifreniz güncellendi.' });
    } catch (err: any) {
      setPasswordMessage({ type: 'error', text: err.message || 'Şifre güncellenemedi.' });
    } finally {
      setPasswordSaving(false);
    }
  };

  const fetchApiProviders = async () => {
    setApiLoading(true);
    setApiError(null);
    try {
      const response = await apiClient.get('/api/settings/ai-providers');
      setApiProviders(response);
    } catch (err: any) {
      setApiError(err.message || 'API ayarları yüklenemedi.');
    } finally {
      setApiLoading(false);
    }
  };

  const handleUpdateApiKey = async (providerType: number, apiKey: string, customBaseUrl: string | null) => {
    const provider = apiProviders.find(item => item.providerType === providerType);
    if (!provider?.isConfigured && !apiKey.trim()) {
      alert('İlk kurulum için API anahtarı girin.');
      return;
    }

    try {
      await apiClient.post('/api/settings/ai-providers', {
        providerType,
        apiKey: apiKey.trim(),
        customBaseUrl
      });
      alert('API bağlantısı başarıyla kaydedildi.');
      fetchApiProviders();
    } catch (err: any) {
      alert('Hata: ' + (err.message || 'API bağlantısı kaydedilemedi.'));
    }
  };

  const saveThemePreference = () => {
    localStorage.setItem('theme', tempTheme);

    if (tempTheme === 'Açık') {
      document.documentElement.classList.add('light');
    } else if (tempTheme === 'Sistem') {
      if (window.matchMedia('(prefers-color-scheme: light)').matches) {
        document.documentElement.classList.add('light');
      } else {
        document.documentElement.classList.remove('light');
      }
    } else {
      document.documentElement.classList.remove('light');
    }
  };

  const tabs: Array<{ icon: React.ElementType; label: SettingsTab; hint: string }> = [
    { icon: User, label: 'Hesabım', hint: 'Profil ve güvenlik' },
    { icon: Palette, label: 'Görünüm', hint: 'Tema ve arayüz' },
    { icon: Key, label: 'API Bağlantıları', hint: 'AI sağlayıcıları' },
  ];

  const themes = [
    { label: 'Açık', icon: Sun, description: 'Daha ferah ve yüksek kontrastlı.' },
    { label: 'Koyu', icon: Moon, description: 'Varsayılan odaklı çalışma görünümü.' },
    { label: 'Sistem', icon: Monitor, description: 'Cihaz tercihine göre otomatik.' }
  ];

  const configuredCount = apiProviders.filter(provider => provider.isConfigured).length;

  return (
    <div className="flex h-full w-full flex-col overflow-hidden bg-ns-bg-primary text-ns-text-primary md:flex-row">
      <aside className="w-full shrink-0 border-b ns-hairline bg-ns-bg-secondary/60 p-4 backdrop-blur-xl md:h-full md:w-72 md:border-b-0 md:p-5">
        <div className="flex h-full flex-col justify-between gap-5">
          <div className="space-y-5">
            <button
              onClick={onBack}
              className="inline-flex items-center gap-2 rounded-xl px-2 py-1.5 text-sm font-medium text-ns-text-secondary transition-colors hover:bg-ns-surface-hover/70 hover:text-ns-text-primary"
            >
              <ArrowLeft className="h-4 w-4" />
              Panoya dön
            </button>

            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-ns-text-muted">Ayarlar</p>
              <h1 className="mt-1 text-xl font-semibold text-ns-text-primary">Hesabım ve Tercihler</h1>
            </div>

            <nav className="flex gap-2 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0">
              {tabs.map((item) => (
                <button
                  key={item.label}
                  onClick={() => setActiveTab(item.label)}
                  className={`flex min-w-fit items-center gap-3 rounded-2xl border px-3 py-3 text-left transition-all ${
                    activeTab === item.label
                      ? 'border-ns-primary/35 bg-ns-primary/12 text-ns-primary shadow-sm shadow-ns-primary/10'
                      : 'border-transparent text-ns-text-secondary hover:border-ns-border/70 hover:bg-ns-surface-hover/70 hover:text-ns-text-primary'
                  }`}
                >
                  <item.icon className="h-4 w-4 shrink-0" />
                  <span className="min-w-0">
                    <span className="block text-sm font-semibold leading-tight">{item.label}</span>
                    <span className="hidden text-xs text-ns-text-muted md:block">{item.hint}</span>
                  </span>
                </button>
              ))}
            </nav>
          </div>

          <button
            onClick={onLogout}
            className="flex w-full items-center justify-center gap-2 rounded-2xl border border-ns-error/20 bg-ns-error/8 px-3 py-2.5 text-sm font-semibold text-ns-error transition-colors hover:bg-ns-error/12 md:justify-start"
          >
            <LogOut className="h-4 w-4" />
            Çıkış Yap
          </button>
        </div>
      </aside>

      <main className="flex-1 overflow-y-auto p-4 sm:p-6 md:p-10">
        <AnimatePresence mode="wait">
          {activeTab === 'Hesabım' && (
            <motion.div
              key="account"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="mx-auto max-w-4xl space-y-5"
            >
              <div>
                <p className="text-sm font-medium text-ns-primary">Profil</p>
                <h2 className="text-2xl font-semibold text-ns-text-primary">Hesap Ayarları</h2>
              </div>

              <section className={sectionClass}>
                <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-4">
                    <div className="h-20 w-20 overflow-hidden rounded-full border border-ns-primary/30 bg-ns-green-surface shadow-lg shadow-ns-primary/10">
                      <img
                        src={`https://api.dicebear.com/9.x/identicon/svg?seed=${selectedAvatar}`}
                        alt="Avatar"
                        className="h-full w-full object-cover"
                      />
                    </div>
                    <div>
                      <h3 className="text-lg font-semibold text-ns-text-primary">{name || 'Kullanıcı'}</h3>
                      <p className="text-sm text-ns-text-secondary">@{username || 'kullanici-adi'}</p>
                      {email && <p className="text-xs text-ns-text-muted">{email}</p>}
                      <div className="mt-2 inline-flex items-center gap-1.5 rounded-full border border-ns-primary/20 bg-ns-primary/10 px-2.5 py-1 text-xs font-semibold text-ns-primary">
                        <ShieldCheck className="h-3.5 w-3.5" />
                        Aktif hesap
                      </div>
                    </div>
                  </div>

                  <button
                    onClick={() => setShowAvatarGrid(!showAvatarGrid)}
                    className="rounded-xl border border-ns-border/75 bg-ns-bg-secondary/70 px-4 py-2 text-sm font-semibold text-ns-text-primary transition-colors hover:border-ns-primary/40 hover:bg-ns-surface-hover/70"
                  >
                    {showAvatarGrid ? 'Vazgeç' : 'Avatar Değiştir'}
                  </button>
                </div>

                <AnimatePresence>
                  {showAvatarGrid && (
                    <motion.div
                      initial={{ opacity: 0, height: 0 }}
                      animate={{ opacity: 1, height: 'auto' }}
                      exit={{ opacity: 0, height: 0 }}
                      className="overflow-hidden"
                    >
                      <div className="mt-5 border-t ns-hairline pt-5">
                        <p className="mb-3 text-xs font-semibold uppercase tracking-wider text-ns-text-muted">Avatar Seçimi</p>
                        <div className="grid grid-cols-4 gap-3 sm:grid-cols-8">
                          {avatarSeeds.map(seed => (
                            <button
                              key={seed}
                              onClick={() => {
                                setSelectedAvatar(seed);
                                localStorage.setItem('avatarSeed', seed);
                                window.dispatchEvent(new Event('avatarChanged'));
                                setShowAvatarGrid(false);
                              }}
                              className={`aspect-square overflow-hidden rounded-full border-2 transition-all ${
                                selectedAvatar === seed
                                  ? 'scale-105 border-ns-primary shadow-lg shadow-ns-primary/20'
                                  : 'border-transparent hover:border-ns-border hover:scale-105'
                              }`}
                            >
                              <img
                                src={`https://api.dicebear.com/9.x/identicon/svg?seed=${seed}`}
                                alt={seed}
                                className="h-full w-full bg-ns-bg-secondary object-cover"
                              />
                            </button>
                          ))}
                        </div>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </section>

              <div className="grid gap-5 lg:grid-cols-[1.05fr_0.95fr]">
                <section className={sectionClass}>
                  <form onSubmit={saveProfile}>
                    <div className="mb-5 flex items-start justify-between gap-3">
                      <div className="flex items-center gap-2">
                        <Sparkles className="h-4 w-4 text-ns-primary" />
                        <div>
                          <h3 className="text-base font-semibold text-ns-text-primary">Profil Bilgileri</h3>
                          <p className="text-xs text-ns-text-muted">Kullanıcı adı ve e-posta kontrolü kaydetme anında yapılır.</p>
                        </div>
                      </div>
                    </div>

                    {profileMessage && (
                      <div className={`mb-4 flex items-start gap-2 rounded-xl border px-3 py-2 text-sm ${
                        profileMessage.type === 'success'
                          ? 'border-ns-primary/20 bg-ns-primary/10 text-ns-primary'
                          : 'border-ns-error/25 bg-ns-error/10 text-ns-error'
                      }`}>
                        {profileMessage.type === 'success' ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />}
                        <span>{profileMessage.text}</span>
                      </div>
                    )}

                    <div className="grid gap-4">
                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">Ad Soyad</span>
                        <input
                          type="text"
                          value={name}
                          onChange={(e) => setName(e.target.value)}
                          className={fieldClass}
                          disabled={profileLoading || profileSaving}
                        />
                      </label>

                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">Kullanıcı Adı</span>
                        <input
                          type="text"
                          value={username}
                          onChange={(e) => setUsername(e.target.value)}
                          className={fieldClass}
                          disabled={profileLoading || profileSaving}
                        />
                      </label>

                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">E-posta</span>
                        <input
                          type="email"
                          value={email}
                          onChange={(e) => setEmail(e.target.value)}
                          className={fieldClass}
                          disabled={profileLoading || profileSaving}
                        />
                      </label>
                    </div>

                    <div className="mt-6 flex justify-end">
                      <button
                        type="submit"
                        disabled={profileLoading || profileSaving}
                        className="inline-flex items-center gap-2 rounded-xl bg-ns-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm shadow-ns-primary/20 transition-colors hover:bg-ns-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        <Save className="h-4 w-4" />
                        {profileSaving ? 'Kaydediliyor...' : 'Profili Kaydet'}
                      </button>
                    </div>
                  </form>
                </section>

                <section className={sectionClass}>
                  <form onSubmit={changePassword}>
                    <div className="mb-5 flex items-center gap-2">
                      <ShieldCheck className="h-4 w-4 text-ns-primary" />
                      <div>
                        <h3 className="text-base font-semibold text-ns-text-primary">Şifre Güvenliği</h3>
                        <p className="text-xs text-ns-text-muted">Şifre değişimi için mevcut şifre doğrulanır.</p>
                      </div>
                    </div>

                    {passwordMessage && (
                      <div className={`mb-4 flex items-start gap-2 rounded-xl border px-3 py-2 text-sm ${
                        passwordMessage.type === 'success'
                          ? 'border-ns-primary/20 bg-ns-primary/10 text-ns-primary'
                          : 'border-ns-error/25 bg-ns-error/10 text-ns-error'
                      }`}>
                        {passwordMessage.type === 'success' ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />}
                        <span>{passwordMessage.text}</span>
                      </div>
                    )}

                    <div className="grid gap-4">
                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">Mevcut Şifre</span>
                        <input
                          type="password"
                          value={currentPassword}
                          onChange={(e) => setCurrentPassword(e.target.value)}
                          className={fieldClass}
                          autoComplete="current-password"
                        />
                      </label>

                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">Yeni Şifre</span>
                        <input
                          type="password"
                          value={newPassword}
                          onChange={(e) => setNewPassword(e.target.value)}
                          className={fieldClass}
                          autoComplete="new-password"
                        />
                      </label>

                      <label className="space-y-1.5">
                        <span className="text-sm font-medium text-ns-text-secondary">Yeni Şifre Tekrar</span>
                        <input
                          type="password"
                          value={confirmPassword}
                          onChange={(e) => setConfirmPassword(e.target.value)}
                          className={fieldClass}
                          autoComplete="new-password"
                        />
                      </label>
                    </div>

                    <div className="mt-4 rounded-2xl border border-ns-border/60 bg-ns-bg-secondary/45 p-3">
                      <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-ns-text-muted">Şifre Koşulları</p>
                      <div className="space-y-2">
                        {passwordRules.map(rule => (
                          <div key={rule.label} className={`flex items-center gap-2 text-xs ${rule.valid ? 'text-ns-primary' : 'text-ns-text-muted'}`}>
                            <CheckCircle2 className={`h-3.5 w-3.5 ${rule.valid ? 'opacity-100' : 'opacity-35'}`} />
                            <span>{rule.label}</span>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div className="mt-6 flex justify-end">
                      <button
                        type="submit"
                        disabled={passwordSaving || !currentPassword || !newPassword || !confirmPassword}
                        className="inline-flex items-center gap-2 rounded-xl bg-ns-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm shadow-ns-primary/20 transition-colors hover:bg-ns-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        <Key className="h-4 w-4" />
                        {passwordSaving ? 'Güncelleniyor...' : 'Şifreyi Güncelle'}
                      </button>
                    </div>
                  </form>
                </section>
              </div>
            </motion.div>
          )}

          {activeTab === 'Görünüm' && (
            <motion.div
              key="appearance"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="mx-auto max-w-4xl space-y-5"
            >
              <div>
                <p className="text-sm font-medium text-ns-primary">Arayüz</p>
                <h2 className="text-2xl font-semibold text-ns-text-primary">Görünüm Ayarları</h2>
              </div>

              <section className={sectionClass}>
                <div className="mb-5">
                  <h3 className="text-base font-semibold text-ns-text-primary">Tema</h3>
                  <p className="text-sm text-ns-text-secondary">Uygulamanın arayüz temasını seçin.</p>
                </div>

                <div className="grid gap-3 sm:grid-cols-3">
                  {themes.map(theme => (
                    <button
                      key={theme.label}
                      onClick={() => setTempTheme(theme.label)}
                      className={`rounded-2xl border p-4 text-left transition-all ${
                        tempTheme === theme.label
                          ? 'border-ns-primary/45 bg-ns-primary/12 text-ns-primary shadow-sm shadow-ns-primary/10'
                          : 'border-ns-border/75 bg-ns-bg-secondary/55 text-ns-text-secondary hover:border-ns-primary/30 hover:bg-ns-surface-hover/70'
                      }`}
                    >
                      <div className="mb-3 flex items-center justify-between">
                        <theme.icon className="h-4 w-4" />
                        {tempTheme === theme.label && <CheckCircle2 className="h-4 w-4" />}
                      </div>
                      <span className="block text-sm font-semibold text-ns-text-primary">{theme.label}</span>
                      <span className="mt-1 block text-xs text-ns-text-muted">{theme.description}</span>
                    </button>
                  ))}
                </div>

                <div className="mt-6 flex justify-end">
                  <button
                    onClick={saveThemePreference}
                    className="inline-flex items-center gap-2 rounded-xl bg-ns-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm shadow-ns-primary/20 transition-colors hover:bg-ns-primary-hover"
                  >
                    <Save className="h-4 w-4" />
                    Tercihleri Kaydet
                  </button>
                </div>
              </section>
            </motion.div>
          )}

          {activeTab === 'API Bağlantıları' && (
            <motion.div
              key="api"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="mx-auto max-w-4xl space-y-5"
            >
              <div>
                <p className="text-sm font-medium text-ns-primary">AI Sağlayıcıları</p>
                <h2 className="text-2xl font-semibold text-ns-text-primary">API Bağlantıları</h2>
              </div>

              <section className={sectionClass}>
                <div className="mb-5 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <h3 className="text-base font-semibold text-ns-text-primary">Sağlayıcılar</h3>
                    <p className="text-sm text-ns-text-secondary">AI modellerini kullanabilmek için API anahtarlarınızı buraya girin.</p>
                  </div>
                  {!apiLoading && !apiError && (
                    <span className="w-fit rounded-full border border-ns-primary/20 bg-ns-primary/10 px-3 py-1 text-xs font-semibold text-ns-primary">
                      {configuredCount} yapılandırıldı
                    </span>
                  )}
                </div>

                {apiLoading && <p className="text-sm text-ns-text-secondary">Yükleniyor...</p>}
                {apiError && <p className="text-sm font-medium text-ns-error">{apiError}</p>}

                {!apiLoading && !apiError && (
                  <div className="space-y-4">
                    {apiProviders.map((provider) => {
                      const providerNames: Record<number, string> = {
                        0: 'OpenAI',
                        1: 'DashScope (Qwen)',
                        2: 'Anthropic',
                        3: 'Gemini',
                        4: 'DeepSeek',
                        5: 'OpenRouter / Özel Sunucu',
                        6: 'Grok'
                      };
                      const providerName = providerNames[provider.providerType] || 'Bilinmeyen Sağlayıcı';

                      return (
                        <div key={provider.providerType} className="ns-glass-soft rounded-2xl p-4">
                          <div className="mb-4 flex items-center justify-between gap-3">
                            <h4 className="font-semibold text-ns-text-primary">{providerName}</h4>
                            {provider.isConfigured ? (
                              <span className="rounded-full bg-ns-primary/10 px-2.5 py-1 text-xs font-semibold text-ns-primary">Yapılandırıldı</span>
                            ) : (
                              <span className="rounded-full bg-ns-text-muted/10 px-2.5 py-1 text-xs font-medium text-ns-text-secondary">Yapılandırılmadı</span>
                            )}
                          </div>

                          <form onSubmit={(e) => {
                            e.preventDefault();
                            const target = e.target as any;
                            handleUpdateApiKey(provider.providerType, target.apiKey.value, target.customBaseUrl?.value || null);
                          }} className="space-y-3">
                            <label className="space-y-1.5">
                              <span className="text-xs font-medium text-ns-text-secondary">API Anahtarı</span>
                              <input
                                name="apiKey"
                                type="password"
                                placeholder={provider.isConfigured ? `Değiştirmek için yazın: ${provider.maskedApiKey}` : 'API anahtarınızı girin'}
                                className={fieldClass}
                              />
                            </label>

                            {provider.providerType === 5 && (
                              <label className="space-y-1.5">
                                <span className="text-xs font-medium text-ns-text-secondary">Özel Base URL</span>
                                <input
                                  name="customBaseUrl"
                                  type="text"
                                  defaultValue={provider.customBaseUrl || ''}
                                  placeholder="Boşsa OpenRouter varsayılanı kullanılır"
                                  className={fieldClass}
                                />
                                <span className="block text-[11px] leading-4 text-ns-text-muted">
                                  Model ID seçimi AI panelindeki model ayarlarından yapılır.
                                </span>
                              </label>
                            )}

                            <div className="flex justify-end pt-1">
                              <button type="submit" className="rounded-xl bg-ns-primary px-4 py-2 text-sm font-semibold text-white shadow-sm shadow-ns-primary/20 transition-colors hover:bg-ns-primary-hover">
                                Kaydet
                              </button>
                            </div>
                          </form>
                        </div>
                      );
                    })}
                  </div>
                )}
              </section>
            </motion.div>
          )}
        </AnimatePresence>
      </main>
    </div>
  );
};
