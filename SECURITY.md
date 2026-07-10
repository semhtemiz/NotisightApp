# Security Policy

## Reporting a Vulnerability

Please do not open public issues for security vulnerabilities.

Report sensitive findings privately to the repository owner. Include:

- A short description of the issue
- Steps to reproduce
- Affected endpoint, file, or deployment surface
- Whether any credential, token, or private user data may be exposed

## Secrets

Do not commit real credentials to this repository. Use environment variables, GitHub Secrets, Vercel Environment Variables, and Azure App Service application settings.

Required production secrets include:

- `ConnectionStrings__DefaultConnection=sql_server_connection_string`
- `Jwt__SigningKey=jwt_signing_key`
- `Gemini__ApiKey=gemini_api_key`
- `Qdrant__Url=qdrant_url` or `Qdrant__Endpoint=qdrant_endpoint`
- `Qdrant__ApiKey=qdrant_api_key`
- `CloudflareR2__BucketName=cloudflare_r2_bucket_name`
- `CloudflareR2__AccessKey=cloudflare_r2_access_key`
- `CloudflareR2__SecretKey=cloudflare_r2_secret_key`
- `CloudflareR2__EndpointUrl=cloudflare_r2_endpoint_url`
- `CloudflareR2__PublicUrlPrefix=cloudflare_r2_public_url_prefix`
- `Deepgram__ApiKey=deepgram_api_key`

GitHub Actions deployment also requires:

- Secret: `AZURE_CLIENT_ID`
- Secret: `AZURE_TENANT_ID`
- Secret: `AZURE_SUBSCRIPTION_ID`
- Variable: `AZURE_WEBAPP_NAME=azure_webapp_name`
- Variable: `AZURE_RESOURCE_GROUP=azure_resource_group`
- Optional variable: `VITE_API_URL=https://api.example.com`

If a secret is ever committed, rotate it in the provider first, then remove it from git history before making the repository public.
