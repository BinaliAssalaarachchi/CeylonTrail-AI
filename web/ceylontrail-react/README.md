# CeylonTrail AI React application

This Vite application contains the shared React authentication and application-shell foundation.

## Local setup

1. Copy `.env.example` to `.env.local`.
2. Keep `VITE_API_BASE_URL` pointed at the running ASP.NET Core API.
3. Install dependencies with `npm install`.
4. Start the development server with `npm run dev`.

The login page uses the backend `/api/auth/login` endpoint. The backend remains authoritative for authentication and authorization.
