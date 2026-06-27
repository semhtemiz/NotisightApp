# Faz 3 Checklist: Yapay Zeka ve RAG Entegrasyonu

## Gemini

- [x] Gemini chat/LLM entegrasyonu calisiyor.
- [x] Gemini embedding entegrasyonu calisiyor.
- [x] API key local secret olarak okunuyor.
- [x] Gemini/Qdrant local secret kurulumu dokumante edildi.
- [x] Kota ve gecici hata senaryolari loglaniyor.

## Chunk ve Embedding

- [x] `TextChunkingService` hazir.
- [x] 512 token hedefli chunk stratejisi uygulandi.
- [x] Yuzde 20 overlap uygulandi.
- [x] `EmbeddingService` hazir.
- [x] Bos/kisa metin durumlari ele alindi.

## Qdrant

- [x] Qdrant client/service hazir.
- [x] `notisight_chunks` collection yoksa otomatik olusuyor.
- [x] `noteId` ve `userId` payload indexleri otomatik olusuyor.
- [x] Vector size Gemini embedding modeliyle uyumlu.
- [x] `UpsertChunks` calisiyor.
- [x] `DeleteByNoteId` calisiyor.
- [x] Semantic search `topK=5` ile calisiyor.

## PDF ve Ses

- [x] PdfPig paketi eklendi.
- [x] `PdfIngestionService` PDF metni cikariyor.
- [x] PDF dosya tipi ve boyut validasyonu var.
- [x] PDF/audio metni MSSQL `notes.content` alaninda saklaniyor.
- [x] `AudioTranscriptionService` WebM/WAV dosyalarini metne ceviriyor.
- [x] Ses dosya tipi ve boyut validasyonu var.

## Endpointler

- [x] `POST /notes/upload-pdf` calisiyor.
- [x] `POST /notes/upload-audio` calisiyor.
- [x] `POST /ai/ask` calisiyor.
- [x] `POST /ai/ask` SSE streaming donduruyor.
- [x] Cevap kaynak `noteId` referanslarini donduruyor.

## Not Yasam Dongusu

- [x] Not olusturma embedding akisini tetikliyor.
- [x] Not guncelleme eski Qdrant chunklarini silip yenilerini ekliyor.
- [x] Not silme SQL kaydi ve Qdrant chunklarini temizliyor.
- [x] Dosyali not silme MSSQL not kaydini ve Qdrant chunklarini temizliyor.
- [x] Hata durumunda tutarsiz veri kalma riski ele alindi.

## RAG Kalitesi

- [x] Ilgili sorularda dogru notlardan kaynak bulunuyor.
- [x] Alakasiz sorularda model baglam yetersizligini belirtiyor.
- [x] Prompt sadece verilen baglama dayanmayi zorunlu kiliyor.
- [x] Retry/backoff uygulanmis.

## Faz Kapanisi

- [x] Backend testleri basarili.
- [x] PDF ingestion manuel test edildi.
- [x] Ses transkripsiyon manuel test edildi.
- [x] RAG soru-cevap manuel test edildi.
- [x] `docs/walkthroughs/phase-3.md` tamamlandi.
- [ ] Faz 4 icin onay alindi.

## Faz 3 Sonrasi Opsiyonel

- [ ] Semantic Kernel paketleri degerlendirildi; mevcut Faz 3 akisi Gemini REST ile calisiyor.
