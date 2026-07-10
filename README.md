# Notisight: Multi-Modal RAG Tabanlı Kişisel AI Not Asistanı

Notisight; notlarınızı, PDF belgelerinizi ve ses kayıtlarınızı tek bir çalışma alanında toplar. İçerikleri işler, anlamlandırır ve kendi verileriniz üzerinden kaynaklı cevaplar verebilen yapay zeka deneyimi sunar.

🔗 **Demo URL:** [https://example.com](https://example.com)

## Öne Çıkanlar

- Zengin metin editörü ile not oluşturma
- PDF ve ses dosyası yükleme
- Ses kayıtlarını otomatik metne dönüştürme
- Notlar üzerinden kaynaklı AI cevapları
- Serbest AI sohbet modu
- Klasör bazlı bilgi yönetimi
- Çoklu AI sağlayıcı yapılandırması
- Semantik arama ve RAG tabanlı yanıt üretimi

## AI ve RAG Mimarisi

Notisight klasik bir not uygulamasından farklı olarak içerikleri sadece saklamaz; onları aranabilir, anlamlandırılabilir ve yapay zeka ile konuşulabilir hale getirir.

Uygulama **Retrieval-Augmented Generation** yaklaşımıyla çalışır:

1. Kullanıcı not, PDF veya ses içeriği ekler.
2. PDF metni çıkarılır, ses dosyası transkribe edilir.
3. İçerik anlamlı parçalara ayrılır.
4. Her parça için embedding üretilir.
5. Embedding verileri Qdrant üzerinde indekslenir.
6. Kullanıcı soru sorduğunda ilgili parçalar semantik olarak bulunur.
7. AI modeli cevabı bu bağlama dayanarak üretir.
8. Cevapta ilgili kaynak notlar referans olarak gösterilir.

Bu yapı sayesinde Notisight, kullanıcının kendi bilgi havuzu üzerinde çalışan kişisel bir AI asistanına dönüşür.

```text
Note / PDF / Audio
        ↓
Text Extraction / Transcription
        ↓
Chunking
        ↓
Embedding
        ↓
Qdrant Vector Search
        ↓
Context Retrieval
        ↓
LLM Answer with Sources
```

## Teknoloji Stack

| Katman | Teknolojiler |
|---|---|
| Frontend | React, Vite, TypeScript, Tailwind CSS, TipTap |
| Backend | .NET 8, ASP.NET Core Web API, EF Core |
| Database | SQL Server |
| Vector Database | Qdrant |
| Object Storage | Cloudflare R2 |
| Speech-to-Text | Deepgram |
| AI | Gemini, OpenAI-compatible providers |
| Deploy | Vercel, Azure App Service, GitHub Actions |

## Kurulum

Backend:

```bash
cd backend/src/Notisight.Api
dotnet run
```

Frontend:

```bash
cd frontend
npm install
npm run dev
```

Gerekli temel ortam ayarları:

```text
VITE_API_URL=https://api.example.com
ConnectionStrings__DefaultConnection=sql_server_connection_string
Jwt__SigningKey=jwt_signing_key
Gemini__ApiKey=gemini_api_key
Qdrant__Url=qdrant_url
Qdrant__ApiKey=qdrant_api_key
CloudflareR2__AccessKey=cloudflare_r2_access_key
CloudflareR2__SecretKey=cloudflare_r2_secret_key
Deepgram__ApiKey=deepgram_api_key
```

## Deploy

Frontend Vercel üzerinde, backend Azure App Service üzerinde çalışır.  
`main` branch’e yapılan güncellemeler GitHub Actions ile backend deploy sürecini tetikler.

## Durum

Notisight aktif olarak geliştirilmektedir. Amaç; kişisel bilgi yönetimi, AI destekli arama, kaynaklı cevap üretimi ve çoklu veri formatı desteğini tek bir modern çalışma alanında birleştirmektir.
