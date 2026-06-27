# Notisight

Notisight; metin, PDF ve ses formatindaki notlari tek bir ortamda tutan, bu notlar uzerinde yapay zeka destekli anlamsal arama ve soru-cevap deneyimi sunan kisisel not asistanidir.

Uygulama multimodal Retrieval-Augmented Generation (RAG) mimarisi uzerine kurulacaktir. Kullanici notlari once veritabaninda saklanir, ardindan metin parcalarina ayrilip embedding uretilerek Qdrant uzerinde aranabilir hale getirilir. Soru-cevap akisinda ilgili kaynak parcalar bulunur ve LLM cevabi yalnizca bu baglama dayanarak uretir.

## Mimari Kararlar

- Monorepo yapisi kullanilacaktir.
- Gelistirme akisi iki branch ile yurutulecektir:
  - `main`: Onaylanmis production adayi surumler.
  - `dev`: Tum aktif gelistirmeler ve testler.
- Hassas bilgiler repoya alinmayacaktir. Local ayarlar `appsettings.Development.json` ve `.env.development` dosyalarinda tutulacak, ornek dosyalar ise repoda yer alacaktir.
- Her faz sonunda teknik testler, UI dogrulamasi ve kisa walkthrough belgesi tamamlanmadan sonraki faza gecilmeyecektir.

## Teknoloji Yigini

- Backend: .NET 8 ASP.NET Core Web API, Entity Framework Core 8
- LLM / Embedding: Gemini 2.5 Flash ve Gemini embedding modeli
- Veritabani: Microsoft SQL Server
- Not ve cikarilmis dosya metni: Microsoft SQL Server
- Vektor Veritabani: Qdrant Cloud
- Frontend: Vite, React, TypeScript, Tailwind CSS
- PDF Isleme: UglyToad.PdfPig
- Auth: JWT access token, refresh token rotation, BCrypt

## Hedef Klasor Yapisi

```text
notisight/
  backend/
    src/
    tests/
  frontend/
    src/
    components/
    lib/
  docs/
    checklists/
    walkthroughs/
  .gitignore
  README.md
```

## Dokumantasyon

- [Uygulama Plani](docs/implementation-plan.md)
- [AI/RAG Local Secret Kurulumu](docs/setup-ai-rag-secrets.md)
- [Faz 1 Checklist](docs/checklists/phase-1-infrastructure.md)
- [Faz 2 Checklist](docs/checklists/phase-2-backend-core.md)
- [Faz 3 Checklist](docs/checklists/phase-3-ai-rag.md)
- [Faz 4 Checklist](docs/checklists/phase-4-frontend.md)
- [Faz 5 Checklist](docs/checklists/phase-5-deploy-test-demo.md)
