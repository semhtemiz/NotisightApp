# Faz 3 Walkthrough

## Tamamlanan Kapsam

- Faz 3 backend iskeleti eklendi.
- `Gemini`, `Qdrant` ve `Rag` option bolumleri appsettings dosyalarina baglandi.
- `TextChunkingService` eklendi; hedef chunk boyutu ve overlap `RagOptions` uzerinden yonetiliyor.
- `ChunkSearchService` eklendi; gercek vektor arama gelene kadar development fallback olarak notlar icinde lexical retrieval yapiyor.
- `RagAnswerService` eklendi; bulunan chunk'lari kullanarak kaynak referansli bir cevap uretiyor.
- `POST /ai/ask` endpointi eklendi; SSE (`text/event-stream`) ile parcali cevap ve `noteId` kaynak referanslari donuyor.
- `PdfIngestionService` ve `POST /notes/upload-pdf` endpointi eklendi.
- `AudioTranscriptionService` ve `POST /notes/upload-audio` endpointi eklendi; Gemini `generateContent` inline audio ile WAV/WEBM transkripsiyon uretiyor.
- `GeminiEmbeddingService` eklendi; API key varsa Gemini `embedContent`, yoksa test/development icin deterministik local embedding uretiyor.
- `GeminiChatService` eklendi; API key varsa bulunan baglamdan Gemini `generateContent` ile kaynakli cevap uretiyor.
- Gemini model varsayilanlari `gemini-2.5-flash` ve `gemini-embedding-001` olarak guncellendi; embedding cikisi Qdrant icin 768 boyuta sabitleniyor.
- Qdrant REST entegrasyonu eklendi; collection bootstrap, `noteId`/`userId` payload indexleri, chunk upsert, note bazli delete ve vector search akisleri baglandi.
- Gemini ve Qdrant HTTP istekleri icin 429/5xx durumlarinda kisa retry/backoff ve warning loglari eklendi.
- Alakasiz vector search sonuclari icin `Rag:MinVectorScore` esigi eklendi; esik alti sonuclarda kontrollu baglam yetersiz cevabi donuyor.
- Not create/update/delete ve PDF/audio upload sonrasi vector sync tetikleniyor.
- Vector sync durumu MSSQL `notes` tablosunda `VectorSyncStatus`, `VectorSyncError` ve `VectorSyncedAtUtc` alanlariyla izleniyor.
- Create/update sonrasi Qdrant sync basarisiz olursa not MSSQL'de kalir ve `failed` olarak isaretlenir; delete akisi Qdrant temizligi basarisizsa SQL kaydini silmez.
- Qdrant note lifecycle sync icin test double eklendi; create/update/delete akisi dis servise baglanmadan dogrulaniyor.
- Gemini/Qdrant local secret kurulumu `docs/setup-ai-rag-secrets.md` icinde dokumante edildi.

## Dogrulama

- `dotnet restore Notisight.slnx --configfile NuGet.Config` basarili.
- `dotnet build Notisight.slnx --no-restore -c Release` basarili.
- `dotnet test backend/tests/Notisight.Api.Tests/Notisight.Api.Tests.csproj --no-restore -c Release` basarili.
- Gercek Gemini + Qdrant Cloud ayarlariyla `AiStreamingTests` smoke testi basarili.
- Manuel smoke testte `sample.pdf` ve `sample.wav` ile PDF upload, audio transcription, ilgili RAG soru-cevap, alakasiz soru ve PDF delete cleanup dogrulandi.
- Guncel toplam 9 test geciyor:
  - Auth lifecycle
  - CRUD ve user isolation
  - JWT token service smoke test
  - AI SSE response smoke test
  - PDF upload validation test
  - Audio upload validation test
  - Audio upload transcript/index success test
  - Note vector lifecycle sync test
  - Vector sync failure persistence test

## Bilinen Notlar

- Semantic Kernel paketi Faz 3 icin zorunlu degil; Gemini entegrasyonu su an REST servisleri uzerinden calisiyor.
- Qdrant `Url` bos oldugunda `POST /ai/ask` development fallback retrieval uzerinden calisir; Qdrant ayarlandiginda once vector search denenir.
- `POST /notes/upload-audio` Gemini API key olmadan calistirilirsa kontrollu 400 dondurur.
- PDF/audio kaynaklarindan cikarilan metin MSSQL `notes.content` alaninda tutuluyor; ayri dosya storage katmani kullanilmiyor.

## Sonraki Faz 3 Adimlari

- Manuel PDF upload ve RAG soru-cevap smoke test yapmak
