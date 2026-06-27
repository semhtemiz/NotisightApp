# Faz 1 Walkthrough

## Tamamlanan Kapsam

- Monorepo klasor yapisi kuruldu.
- `backend/` altinda .NET 8 Web API projesi olusturuldu.
- `backend/tests/` altinda xUnit test projesi olusturuldu ve solution'a baglandi.
- `frontend/` altinda Next.js 14 App Router, TypeScript ve Tailwind CSS projesi olusturuldu.
- Backend icin `appsettings.Example.json` eklendi.
- Frontend icin `.env.example` ve `.env.development` eklendi.
- Klasor kokunde `.gitignore`, `NuGet.Config` ve solution dosyasi hazirlandi.
- Kok git deposu `dev` branch ile baslatildi.

## Dogrulama

- `dotnet restore Notisight.slnx` basarili.
- `dotnet build Notisight.slnx --no-restore` basarili.
- `npm run lint` basarili.
- `npm run build` basarili.
- `http://127.0.0.1:5047/health` endpoint'i `Healthy` yaniti verdi.
- `http://127.0.0.1:3000` endpoint'i `200` yaniti verdi.

## Bilinen Notlar

- `live` branch ilk commit olusmadan acilamiyor. Ilk commit sonrasinda branch olusturulacak.
- Backend dev server ve frontend dev server sandbox disi izinle calistirildi.
- Swagger arayuzu dogrudan manuel acilmadi, ancak API proje calisma ve build dogrulamasi tamamlandi.

## Sonraki Faz

Faz 2 icin siradaki hedef; veri modeli, EF Core, auth akisi ve temel CRUD iskeletini kurmak.
