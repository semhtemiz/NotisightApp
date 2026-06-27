# 11 - API Endpointleri

## Genel

Backend controller'lari REST JSON ve AI icin SSE stream kullanir. Yetkili endpointlerin buyuk bolumu JWT gerektirir.

## Auth

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| POST | `/auth/register` | Hayir | Kullanici kaydi |
| POST | `/auth/login` | Hayir | Email veya username ile giris |
| POST | `/auth/refresh` | Hayir | Token yenileme |
| POST | `/auth/logout` | Hayir | Refresh token iptali |
| GET | `/auth/me` | Evet | Aktif kullanici |
| PUT | `/auth/profile` | Evet | Profil guncelleme |
| PUT | `/auth/password` | Evet | Parola degistirme |

## Notes ve Ingestion

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| GET | `/notes` | Evet | Kullanici notlari |
| GET | `/notes/{id}` | Evet | Tek not |
| POST | `/notes` | Evet | Not olusturma |
| PUT | `/notes/{id}` | Evet | Not guncelleme |
| DELETE | `/notes/{id}` | Evet | Not ve vektor temizleme |
| GET | `/notes/{id}/file` | Evet | PDF/ses dosya stream |
| POST | `/notes/upload-pdf` | Evet | PDF ingestion |
| POST | `/notes/upload-audio` | Evet | Audio ingestion |
| POST | `/notes/{noteId}/attachments` | Evet | Gorsel attachment |
| GET | `/notes/attachments/{id}/file` | Hayir | Attachment file stream |

## Folders

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| GET | `/folders` | Evet | Klasor listesi |
| POST | `/folders` | Evet | Klasor olusturma |
| PUT | `/folders/{id}` | Evet | Klasor guncelleme |
| DELETE | `/folders/{id}` | Evet | Klasor silme, not/alt klasorleri root'a alma |

## Tags

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| GET | `/tags` | Evet | Etiket listesi |
| POST | `/tags` | Evet | Etiket olusturma |
| PUT | `/tags/{id}` | Evet | Etiket guncelleme |
| DELETE | `/tags/{id}` | Evet | Etiket silme ve note_tags temizleme |

## AI

| Method | Endpoint | Auth | Response | Aciklama |
|---|---|---|---|---|
| POST | `/ai/ask` | Evet | `text/event-stream` | Standard veya Notisight modunda streaming cevap |
| POST | `/ai/generate-title` | Evet | JSON | Ilk sorudan chat basligi uretme |
| GET | `/ai/sessions` | Evet | JSON | Chat session listesi |
| GET | `/ai/sessions/{sessionId}/messages` | Evet | JSON | Session mesajlari |
| DELETE | `/ai/sessions/{sessionId}` | Evet | 204 | Session silme |
| GET | `/ai/tones` | Hayir | JSON | Ton profilleri |

## Settings

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| GET | `/api/settings/ai-providers` | Evet | Kullanici provider ayarlari ve masked key |
| POST | `/api/settings/ai-providers` | Evet | API key/custom URL kaydetme |

## Health

| Method | Endpoint | Auth | Aciklama |
|---|---|---|---|
| GET | `/health` | Hayir | ASP.NET health check |

## AI Ask Request Ozeti

| Alan | Tip | Aciklama |
|---|---|---|
| `question` | string | Kullanici sorusu |
| `sessionId` | string? | Chat session id |
| `mode` | number | 0 Standard, 1 Notisight |
| `tone` | number | 0 Casual, 1 Technical, 2 Pedagogical, 3 Formal |
| `provider` | number | Provider enum |
| `modelId` | string? | Secili model |
| `history` | array? | Onceki mesajlar |

## Endpoint Akis Ozeti

```mermaid
flowchart LR
    FE[Frontend] --> AUTH[/auth/*]
    FE --> CRUD[/notes /folders /tags]
    FE --> ING[/notes/upload-*]
    FE --> AI[/ai/ask SSE]
    FE --> SET[/api/settings/*]
```
