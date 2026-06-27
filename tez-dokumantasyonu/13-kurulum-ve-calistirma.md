# 13 - Kurulum ve Calistirma

## On Kosullar

| Arac | Amac |
|---|---|
| .NET 8 SDK | Backend build/test/run |
| Node.js + npm | Frontend dependency ve Vite |
| SQL Server | Local veya remote veritabani |
| Qdrant Cloud veya local Qdrant | Vektor arama |
| Gemini API key | Embedding ve ses transkripsiyonu |
| Cloudflare R2 credentials | Dosya depolama |

## Backend Calistirma

```powershell
cd backend/src/Notisight.Api
dotnet restore
dotnet run
```

Varsayilan frontend API client'i `http://localhost:5000` adresini kullanir. Farkli port kullaniliyorsa frontend icin `VITE_API_URL` set edilmelidir.

## Frontend Calistirma

```powershell
cd frontend
npm install
npm run dev
```

Vite script'i port 3000 ve host `0.0.0.0` ile calisir.

## Test Calistirma

```powershell
dotnet test backend/tests/Notisight.Api.Tests/Notisight.Api.Tests.csproj
```

## Ortam Degiskenleri

Gercek degerler asla dokumana veya repoya yazilmamalidir. Asagidaki liste yalnizca alan adlarini gosterir.

### Backend

```text
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True
Jwt__Issuer=...
Jwt__Audience=...
Jwt__SigningKey=...
Jwt__AccessTokenMinutes=60
Jwt__RefreshTokenDays=14
Gemini__ApiKey=...
Gemini__ChatModel=gemini-2.5-flash
Gemini__EmbeddingModel=gemini-embedding-001
Qdrant__Url=https://...
Qdrant__ApiKey=...
Qdrant__CollectionName=notisight_chunks
Qdrant__VectorSize=768
Rag__ChunkTokenTarget=300
Rag__ChunkOverlapPercent=20
Rag__TopK=8
CloudflareR2__BucketName=...
CloudflareR2__AccessKey=...
CloudflareR2__SecretKey=...
CloudflareR2__EndpointUrl=https://...
CloudflareR2__PublicUrlPrefix=https://...
```

### Frontend

```text
VITE_API_URL=http://localhost:5000
```

## Local Secret Yonetimi

.NET user-secrets veya process environment variable kullanilabilir. `appsettings.Development.json` local makineye ozel kalmali, repoya hassas bilgi ile commit edilmemelidir.

## Veritabani

Uygulama acilista migration calistirir. Testlerde migration atlanir ve SQLite in-memory schema olusturulur.

| Ayar | Davranis |
|---|---|
| `Database:SkipMigrations=false` | `Migrate()` calisir |
| `Database:SkipMigrations=true` | Migration atlanir |

## Ilk Kullanim Akisi

1. Backend ve frontend calistirilir.
2. Kullanici kayit olur.
3. Ayarlar ekraninda AI provider API anahtari girilir.
4. PDF, ses veya metin notu olusturulur.
5. Notisight modunda AI panelinden soru sorulur.

## Sorun Giderme

| Belirti | Kontrol |
|---|---|
| AI cevap vermiyor | Provider API key ve model id kontrol edilir |
| RAG sonuc yok | Qdrant URL/API key/collection ve note vector status kontrol edilir |
| Ses upload hatasi | Gemini API key ve dosya boyutu kontrol edilir |
| PDF gorunmuyor | R2 file URL ve `/notes/{id}/file` response kontrol edilir |
| 401 dongusu | Refresh token ve localStorage temizlenir |
