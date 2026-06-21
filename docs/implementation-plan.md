# Notisight Uygulama Plani

Bu belge Notisight projesini uygulanabilir teknik gorevlere ayirir. Fazlar sirasiyla ilerler, ancak Faz 4 frontend gelistirmesi Faz 3 ile kontrollu bicimde paralel yurutulebilir.

## 1. Faz: Altyapi ve Hazirlik

### Hedef

Monorepo iskeletinin, backend/frontend projelerinin ve ortam ayriminin kurulmasi.

### Gorevler

1. Git deposu olusturulur ve `dev` ile `live` branch akisi tanimlanir.
2. Kok dizinde `.gitignore`, `README.md`, `docs/` ve temel klasor yapisi hazirlanir.
3. `backend/` altinda .NET 8 ASP.NET Core Web API projesi olusturulur.
4. `frontend/` altinda Next.js 14 App Router, TypeScript ve Tailwind CSS projesi olusturulur.
5. Backend icin `appsettings.Development.json` local kullanima ayrilir.
6. Frontend icin `.env.development` local kullanima ayrilir.
7. Repoya hassas veri girmeden `.env.example` ve `appsettings.example.json` sablonlari eklenir.

### Baslangic Ortam Degiskenleri

Backend:

```text
ConnectionStrings__DefaultConnection=
Jwt__Issuer=
Jwt__Audience=
Jwt__SigningKey=
Gemini__ApiKey=
Qdrant__Endpoint=
Qdrant__ApiKey=
Qdrant__CollectionName=notisight_chunks
Qdrant__VectorSize=768
```

Frontend:

```text
NEXT_PUBLIC_API_URL=
```

## 2. Faz: Backend Cekirdegi, Veritabani ve Auth

### Hedef

Kullanici kimligi, temel veri modeli, migration ve CRUD altyapisinin calisir hale getirilmesi.

### Paketler

- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `BCrypt.Net-Next`
- `Swashbuckle.AspNetCore`
- `Microsoft.AspNetCore.RateLimiting`

### Entity Modeli

- `User`
- `Folder`
- `Note`
- `Tag`
- `NoteTag`
- `RefreshToken`

### Iliski Notlari

- `User` birden fazla `Folder`, `Note`, `Tag` ve `RefreshToken` sahibi olur.
- `Folder` hiyerarsisi icin `ParentFolderId` ile self-referencing iliski kurulur.
- `Note` opsiyonel olarak bir `Folder` icinde tutulur.
- `NoteTag`, `Note` ve `Tag` arasindaki many-to-many baglantidir.
- `RefreshToken` token rotation ve iptal akisi icin kullanilir.

### Auth Endpointleri

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`

### CRUD Endpointleri

- `GET /folders`
- `POST /folders`
- `PUT /folders/{id}`
- `DELETE /folders/{id}`
- `GET /notes`
- `GET /notes/{id}`
- `POST /notes`
- `PUT /notes/{id}`
- `DELETE /notes/{id}`
- `GET /tags`
- `POST /tags`
- `PUT /tags/{id}`
- `DELETE /tags/{id}`

### Teknik Kabul Kriterleri

- EF Core migration SQL Server uzerinde calisir.
- JWT middleware kullanici kimligini guvenilir bicimde cozer.
- Kullanici sadece kendi not, klasor ve etiketlerine erisir.
- Refresh token rotation onceki refresh tokeni gecersiz kilar.
- Merkezi hata yonetimi standart response formati dondurur.
- Rate limiting auth ve AI endpointleri icin ayri kurallarla hazirlanir.

## 3. Faz: Yapay Zeka ve RAG Entegrasyonu

### Hedef

Not iceriklerinin chunk, embedding, vektor indeksleme ve kaynakli soru-cevap akisina baglanmasi.

### Servisler

- `EmbeddingService`: Metin parcalari icin Gemini embedding uretir.
- `TextChunkingService`: Yaklasik 512 token ve yuzde 20 overlap ile chunk uretir.
- `QdrantVectorService`: Collection olusturma, upsert, semantic search ve note bazli silme islemlerini yapar.
- `PdfIngestionService`: PDF metnini PdfPig ile cikarir ve not icerigi olarak MSSQL'e kaydeder.
- `AudioTranscriptionService`: WebM/WAV ses dosyalarini Gemini 2.5 Flash inline audio ile metne donusturur.
- `RagAnswerService`: Semantic search sonucunu prompt baglamina donusturur ve streaming cevap uretir.

### Qdrant Collection

Collection adi:

```text
notisight_chunks
```

Payload alanlari:

```text
userId
noteId
chunkId
title
content
sourceType
createdAt
updatedAt
```

### Endpointler

- `POST /notes/upload-pdf`
- `POST /notes/upload-audio`
- `POST /ai/ask`

### Not Yasam Dongusu

- Not olusturuldugunda chunk ve embedding uretilebilir.
- Not guncellendiginde eski Qdrant chunklari silinir, yeni chunklar eklenir.
- Not silindiginde MSSQL kaydi ve Qdrant chunklari temizlenir.
- PDF veya ses yukleme basarili olursa sonuc bir not olarak kaydedilir.

### RAG Cevap Kurallari

- Semantic search `topK=5` ile calisir.
- Prompt, modeli yalnizca verilen baglamdan cevaplamaya zorlar.
- Baglam yetersizse model bunu acikca belirtir.
- SSE streaming ile anlik cevap akisi saglanir.
- Cevapla birlikte kullanilan `noteId` kaynak referanslari dondurulur.
- Gemini kota asimi ve gecici hatalar icin Polly retry/backoff stratejisi uygulanir.

## 4. Faz: Frontend Gelistirme

### Hedef

Kullanici girisi, not yonetimi, 3 panelli ana arayuz, upload akislar ve AI chat deneyiminin kurulmasi.

### Temel Moduller

- Auth ekranlari: `/login`, `/register`
- Route korumasi: `middleware.ts`
- API client: token ekleme, refresh akisi, logout yonlendirmesi
- Tema: karanlik/aydinlik mod
- Layout: sol not agaci, orta editor, sag AI asistan

### Ana Arayuz

- Sol panel: klasor ve not agaci, ekleme, silme, secme
- Orta panel: baslik, etiketler, markdown editor, debounce auto-save
- Sag panel: AI soru-cevap, streaming cevap, kaynak referanslari

### Dosya ve Ses Akislari

- PDF yukleme paneli
- Upload progress gostergesi
- Tarayici `MediaRecorder` ile ses kaydi
- Kayit sonrasi upload ve transkriptin not olarak acilmasi

### Frontend Kabul Kriterleri

- Kullanici login/register akislarini tamamlayabilir.
- Access token suresi doldugunda refresh akisi sessizce calisir.
- Not olusturma, duzenleme, silme ve klasorleme UI uzerinden yapilir.
- AI chat cevabi streaming olarak akar.
- PDF ve ses yukleme akislari hata ve basari durumlarini gosterir.
- Responsive layout masaustu ve mobilde okunabilir kalir.

## 5. Faz: Canliya Alma, Test ve Sunum Hazirligi

### Hedef

Dev ortaminda tamamlanan surumun live branch, deploy, test ve demo akisi ile teslim edilebilir hale getirilmesi.

### Backend Deploy

- Azure App Service veya secilen alternatif cloud ortamina yayin.
- App settings/secrets production ortaminda tanimlanir.
- CORS sadece gerekli frontend originlerini kabul eder.
- Health check endpointi eklenir.

### Frontend Deploy

- Vercel deploy ayarlanir.
- `NEXT_PUBLIC_API_URL` Vercel environment olarak girilir.
- Auth cookie kullaniliyorsa `SameSite=None` ve `Secure=true` domain uyumu dogrulanir.

### Test Basliklari

- Auth lifecycle
- Refresh token rotation
- CRUD yetkilendirme
- Not auto-save
- PDF ingestion
- Ses transkripsiyon
- Semantic search dogrulugu
- Alakasiz sorulara kontrollu cevap
- SSE streaming
- MSSQL ve Qdrant cleanup

### Demo Hazirligi

- Pilot not verileri hazirlanir.
- PDF ve ses ornekleri eklenir.
- Demo rotasi yazilir.
- Faz tamamlama walkthrough belgesi uretilir.

## Faz Gecis Kurali

Her faz sonunda:

1. Checklist tamamlanir.
2. Otomatik testler calistirilir.
3. UI uzerinden ilgili akis gosterilir.
4. `docs/walkthroughs/phase-N.md` belgesi yazilir.
5. Kullanici onayi alindiktan sonra sonraki faza gecilir.
