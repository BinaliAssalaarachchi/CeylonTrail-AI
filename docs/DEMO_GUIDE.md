# CeylonTrail-AI Demo Guide

This guide uses the current local configuration and the seeded development accounts.
Do not edit the database or source code during the presentation.

## Part 1 - Start the project

Prerequisites: PostgreSQL, .NET 8, Node.js/npm, Python with the `agentic-ai/requirements.txt` packages, and Flutter/Android tooling.

Start PostgreSQL first. The local database is `ceylontrail_db`.

Terminal 1 - Travel Intelligence agent:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
uvicorn travel_intelligence.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8001
```

Terminal 2 - Planner agent:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
uvicorn planner.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8002
```

Terminal 3 - Destination agent:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
uvicorn destination.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8003
```

Terminal 4 - Booking action agent:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
uvicorn bookings.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8004
```

Terminal 5 - ASP.NET Core API:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
dotnet run --project backend\CeylonTrail.Api\CeylonTrail.Api.csproj --launch-profile http
```

The API uses PostgreSQL and the configured user secrets. Confirm `http://localhost:5027/health` returns `Healthy` and `http://localhost:5027/health/ready` returns `{"status":"Healthy"}`.

Terminal 6 - React:

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI\web\ceylontrail-react
npm install
npm run dev
```

If PowerShell blocks `npm.ps1`, use `npm.cmd run dev`.

Terminal 7 - Flutter Android (optional mobile demo):

```powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI\mobile\ceylontrail_flutter
flutter pub get
flutter run
```

The Android emulator reaches the API at `http://10.0.2.2:5027`.

## Part 2 - Open the application

Open the React application at `http://localhost:5173`.

For mobile, start the Android emulator and launch the Flutter application.

## Part 3 - Login

All seeded accounts use password `Test@123`:

| Role | Email |
|---|---|
| Tourist | `tourist@evaluator.com` |
| Tourism Provider | `provider@evaluator.com` |
| Travel Coordinator | `coordinator@evaluator.com` |
| Administrator | `admin@evaluator.com` |

The password for all four evaluator accounts is `Test@123`. The older `@test.com`
accounts are also retained for local backwards compatibility.

Use the Tourist account for the main journey demo, the Provider account for booking management, and the Coordinator or Administrator account for AI review and approvals.

## Part 4 - Main demo workflow

1. Log in as `tourist@evaluator.com`.
2. Open Discover and select an approved attraction, such as Sigiriya Heritage Sunrise Trail.
3. Use the recommendation or attraction detail flow to inspect the seeded attraction, category, location, price, and availability.
4. Open Trips and create a trip with a future start/end date and a budget of at least LKR 50,000.
5. Add an objective or interest, then save the trip.
6. Select Generate itinerary. The request goes through ASP.NET Core to the Planner, Destination, Booking Action, and Travel Intelligence workflow.
7. Open the itinerary and workflow/safety details. Show the stage summaries and any human-approval state.
8. If an approval is pending, log out and log in as `coordinator@evaluator.com` or `admin@evaluator.com`.
9. Open AI Operations, review the proposed action, and approve or reject it with a comment.
10. Return to the tourist account and show the resulting itinerary, workflow status, and booking state.

## Part 5 - CRUD demonstration

### Tourist trip CRUD

1. Click Trips and choose Create trip.
2. Enter `Demo CRUD trip`, future dates, and budget `50000`.
3. Press Create.
4. Expected result: the trip appears in My Trips and is persisted by the API/database.
5. Open the trip, change the name to `Demo CRUD trip updated`, and press Save.
6. Refresh the page. Expected result: the updated name remains.
7. Delete the draft trip and confirm. Expected result: it disappears and a subsequent direct read returns not found.

### Booking CRUD

1. From an attraction with a future availability slot, click Book.
2. Select the future slot and enter one guest.
3. Submit the booking. Expected result: a Draft booking is created with the server-calculated price.
4. Open My Bookings and inspect the booking details/history.
5. Delete the draft booking. Expected result: it is removed from My Bookings.

### Provider booking management

1. Log in as `provider@evaluator.com`.
2. Open the provider booking area.
3. Inspect incoming bookings and use Accept or Reject where the demo booking is available.
4. Expected result: the status and history update through the backend.

## Part 6 - Agent demonstration

1. Use a future-dated trip with objective text such as `heritage and nature experiences in Sri Lanka`.
2. Click Generate itinerary.
3. Show that the browser calls ASP.NET Core, not the Python services directly.
4. Show the generated itinerary and the four workflow stages.
5. Open AI Operations as a Coordinator or Administrator.
6. Expand each stage and show its structured result, proposed action, and approval requirement.
7. Approve or reject the action and show the recorded decision.

Evidence to show: the workflow ID, stage list, database-backed execution/approval status, and the final UI result. Do not claim that an external LLM was used unless the relevant provider environment variables are configured; the deterministic fallback is the supported local path.

## Part 7 - Role demonstration

- Tourist: discover attractions, manage trips, generate itineraries, create/delete draft bookings, and view travel safety results.
- Tourism Provider: manage owned attractions, availability, and incoming bookings.
- Travel Coordinator: inspect operational trips, AI workflows, reports, and approvals.
- Administrator: manage pending attractions, approvals, alerts, and all staff-level operations.

## Part 8 - Backup demo

If an agent service is unavailable, demonstrate authentication, Discover, trip CRUD, attraction availability, and booking draft CRUD. Show the agent health endpoint or the visible service-unavailable message, then continue with the already persisted records. Do not modify source code, manually edit PostgreSQL, or create fake AI responses during the presentation.

If the Flutter emulator is unavailable, use the React application at `http://localhost:5173` and explain that both clients use the same ASP.NET Core API.
