# Faz 2 Checklist: Backend Cekirdegi, Veritabani ve Auth

## Paketler

- [x] EF Core SQL Server paketi eklendi.
- [x] JWT Bearer paketi eklendi.
- [x] BCrypt paketi eklendi.
- [x] Swashbuckle paketi eklendi.
- [x] Rate limiting altyapisi eklendi.

## Veri Modeli

- [x] `User` entity hazir.
- [x] `Folder` entity hazir.
- [x] `Note` entity hazir.
- [x] `Tag` entity hazir.
- [x] `NoteTag` entity hazir.
- [x] `RefreshToken` entity hazir.
- [x] Folder parent-child iliskisi kuruldu.
- [x] Note-Folder iliskisi kuruldu.
- [x] Note-Tag many-to-many iliskisi kuruldu.

## DbContext ve Migration

- [x] `ApplicationDbContext` hazir.
- [x] Entity konfigurasyonlari tamamlandi.
- [x] Ilk migration olusturuldu.
- [x] Migration SQL Server uzerinde calisti.
- [x] Index ve unique constraint'ler kontrol edildi.

## Auth

- [x] `POST /auth/register` calisiyor.
- [x] Sifreler BCrypt ile hashleniyor.
- [x] `POST /auth/login` access ve refresh token donduruyor.
- [x] `POST /auth/refresh` token rotation yapiyor.
- [x] `POST /auth/logout` refresh tokeni iptal ediyor.
- [x] JWT middleware endpointleri koruyor.
- [x] JWT'den `UserId` okunabiliyor.

## CRUD

- [x] Folder listeleme calisiyor.
- [x] Folder ekleme/guncelleme/silme calisiyor.
- [x] Note listeleme calisiyor.
- [x] Note ekleme/guncelleme/silme calisiyor.
- [x] Tag listeleme calisiyor.
- [x] Tag ekleme/guncelleme/silme calisiyor.
- [x] Kullanici izolasyonu test edildi.

## Altyapi

- [x] Global error handling eklendi.
- [x] Standart hata response formati belirlendi.
- [x] Auth endpointleri icin rate limiting eklendi.
- [x] Swagger auth ile test edilebilir hale getirildi.

## Faz Kapanisi

- [x] Backend testleri basarili.
- [ ] Migration temiz ortamda tekrar calisiyor.
- [ ] Auth lifecycle manuel test edildi.
- [ ] CRUD akislar Swagger veya HTTP client ile dogrulandi.
- [x] `docs/walkthroughs/phase-2.md` tamamlandi.
- [ ] Faz 3 icin onay alindi.
