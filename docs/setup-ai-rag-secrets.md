# AI/RAG Local Secret Kurulumu

Bu dosyadaki degerler repoya yazilmaz. Local gelistirmede environment variable veya .NET user-secrets kullan.

## Environment Variable

PowerShell oturumu icin:

```powershell
$env:Gemini__ApiKey="..."
$env:Gemini__ChatModel="gemini-2.5-flash"
$env:Gemini__EmbeddingModel="gemini-embedding-001"
$env:Qdrant__Endpoint="https://your-qdrant-cloud-endpoint"
$env:Qdrant__ApiKey="..."
$env:Qdrant__CollectionName="notisight_chunks"
$env:Qdrant__VectorSize="768"
$env:Rag__MinVectorScore="0.25"
```

`Qdrant__Url` da desteklenir; `Qdrant__Endpoint` verilirse backend ayni degeri kullanir.

## .NET User-Secrets

Backend proje klasorunden:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "..."
dotnet user-secrets set "Gemini:ChatModel" "gemini-2.5-flash"
dotnet user-secrets set "Gemini:EmbeddingModel" "gemini-embedding-001"
dotnet user-secrets set "Qdrant:Endpoint" "https://your-qdrant-cloud-endpoint"
dotnet user-secrets set "Qdrant:ApiKey" "..."
dotnet user-secrets set "Qdrant:CollectionName" "notisight_chunks"
dotnet user-secrets set "Qdrant:VectorSize" "768"
dotnet user-secrets set "Rag:MinVectorScore" "0.25"
```

## Smoke Test

Gercek servislerle hedefli RAG testi:

```powershell
dotnet test backend/tests/Notisight.Api.Tests/Notisight.Api.Tests.csproj -c Release --filter FullyQualifiedName~AiStreamingTests
```

Test veya local calisma basarisiz olursa once su uc noktayi kontrol et:

- Gemini model listesinde `gemini-embedding-001` ve `gemini-2.5-flash` gorunuyor mu?
- Qdrant endpoint HTTPS REST endpointi olarak erisilebilir mi?
- Qdrant API key collection create, payload index create, points upsert ve search yetkilerine sahip mi?
