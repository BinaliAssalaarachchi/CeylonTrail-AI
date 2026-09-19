# CeylonTrail AI

CeylonTrail AI is an SE3090 university project for shared tourism-platform infrastructure. The repository currently contains the shared authentication foundation; business modules are developed on feature branches.

## Technology and architecture

- ASP.NET Core Web API on .NET 8
- Entity Framework Core 8 with PostgreSQL
- JWT authentication and role-based authorization
- React + Vite web foundation
- Flutter mobile foundation
- GitHub Actions backend CI

ASP.NET Core is the authoritative public API. React and Flutter communicate with ASP.NET Core and never access PostgreSQL or internal AI services directly. PostgreSQL is accessed only by the backend through EF Core.

## Repository structure

```text
backend/CeylonTrail.Api/       ASP.NET Core API, EF Core, authentication
tests/CeylonTrail.Api.Tests/   Backend unit tests
web/ceylontrail-react/         React shared foundation
mobile/ceylontrail_flutter/   Flutter shared foundation
docs/adr/                      Architecture Decision Records
agentic-ai/                    Reserved internal AI service area
database/                      Database-related project material
```

## Prerequisites

Install the .NET 8 SDK, PostgreSQL, Node.js/npm, Flutter SDK, and Android tooling for mobile development.

## Local backend setup

Create the PostgreSQL database `ceylontrail_db`, then run these commands from `backend/CeylonTrail.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ceylontrail_db;Username=<local-user>;Password=<local-password>"
dotnet user-secrets set "Jwt:SigningKey" "<local-development-signing-key>"
dotnet run
```

Replace placeholders locally. Never commit these values. In Development, Swagger is available at `/swagger` and the health endpoint is `/health`.

## React setup

```powershell
cd web/ceylontrail-react
Copy-Item .env.example .env.local
npm install
npm run dev
```

The example API URL is `http://localhost:5027`. Local environment files are ignored.

## Flutter setup

```powershell
cd mobile/ceylontrail_flutter
flutter pub get
flutter run
```

Android emulators reach the host API at `http://10.0.2.2:5027`. Override it with `flutter run --dart-define=API_BASE_URL=https://api.example.com` when needed.

## Checks

```powershell
dotnet restore backend/CeylonTrail.Api/CeylonTrail.Api.csproj
dotnet build backend/CeylonTrail.Api/CeylonTrail.Api.csproj
dotnet test tests/CeylonTrail.Api.Tests/CeylonTrail.Api.Tests.csproj
cd web/ceylontrail-react; npm run lint; npm run build
cd ../../mobile/ceylontrail_flutter; flutter analyze; flutter test; flutter build apk --debug
```

Keep `main` stable: use focused feature branches, run relevant checks, and open pull requests. Do not commit secrets or generated output. Trips, attractions, bookings, validation, and AI features are intentionally deferred to separate branches.
