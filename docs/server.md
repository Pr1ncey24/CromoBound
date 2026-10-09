# CromoBound: Server Design (Phase 3, part 1)

Status: approved design, before implementation. Builds on the engine (`docs/engine-architecture.md`, `docs/effects-engine.md`).

## 1. Goal and scope

CromoBound is a Riftbound simulator for a handful of friends. This first part of Phase 3 delivers a hosted server where two signed-in players can play a complete Bo1 or Bo3 against each other, with nothing of the site visible to anyone who isn't signed in.

### In scope
- An ASP.NET Core server (`CromoBound.Server`) hosting the engine.
- Accounts created by an admin, two roles, cookie sign-in, a login page, default-deny authorization, and protection against brute-force logins.
- Admin HTTP endpoints to manage users and a maintenance switch.
- Challenges between players, live matches over SignalR with per-player views, saving after every action, and reloading on restart.
- Server tests and a Docker deployment for a Linux VPS.

### Out of scope (later specs)
- The Phase 4 Blazor WebAssembly UI (apart from the static login page).
- Lobbies, friends lists, saved decks, match history pages.
- Self-service password changes, spectators, turn timers.

### Success criteria
- Signed out, the only thing reachable is the login page and the login endpoint.
- Two accounts can play a full match through the hub, each receiving only their own view.
- A server restart in the middle of a match loses nothing.
- Login brute force is throttled and a lockout reveals nothing.

## 2. Decisions summary

| Topic | Decision |
|---|---|
| Hosting | A small Linux VPS, the app in Docker behind an HTTPS reverse proxy (Caddy or nginx) |
| Storage | SQLite through EF Core |
| Client (Phase 4) | Blazor WebAssembly served by the same server; shares the engine's C# records |
| Accounts | Created by an admin; no sign-up; passwords permanent, changed only by an admin |
| Roles | Player and admin; an admin can do everything a player can; specific role identifiers, never named in responses |
| Sign-in | ASP.NET Core cookie authentication, no ASP.NET Identity system (only its password hasher) |
| Match hosting | One in-memory host per running match behind a per-match lock; record saved after every accepted action |
| First admin | Created from environment variables at startup when there are no users |

## 3. Projects and layout

```
src/CromoBound.Server/
  Program.cs                 composition: auth, rate limiting, EF Core, SignalR, endpoints
  Accounts/                  users, roles, password rules, login, admin endpoints, session validation
  Matches/                   challenges, MatchRegistry, MatchHost, persistence
  Hubs/GameHub.cs            the SignalR hub
  Storage/                   CromoDbContext, entities, migrations
  wwwroot/login.html         the only public page
tests/CromoBound.Server.Tests/   WebApplicationFactory + SignalR client tests
Dockerfile
docs/server-deploy.md
```

- `CromoBound.Server` references `CromoBound.Engine` and `CromoBound.Data`. Card data is loaded once at startup from the configured data folder with `CardRepository` and shared read-only.
- NuGet packages: EF Core SQLite and `Microsoft.Extensions.Identity.Core` (for `PasswordHasher` only). The test project adds the ASP.NET Core test host and the SignalR client. The engine and models keep their no-package rule.

## 4. Security model

### 4.1 Default deny
- A fallback authorization policy requires a signed-in user holding a player role on every endpoint, static file and the hub.
- Only `GET /login` (the page) and `POST /login` allow anonymous access.
- Signed out: browser navigation is redirected to `/login`; API and hub calls get a bare 401.
- Signed in without the right role: a bare 403 (404 where hiding the endpoint is better). No response body or header ever contains a role name or identifier.

### 4.2 Roles
- Two policies, `Seat` (player) and `Steward` (admin). An admin holds both role claims, so admin-implies-player is structural.
- The role claim values are long, specific identifiers defined once in a constants class. They never appear in responses, logs or error messages.
- The real guarantee against fabricated tokens is the cookie itself: encrypted and signed with the server's data-protection keys.

### 4.3 Sessions
- Cookie: HttpOnly, Secure, SameSite=Strict, sliding expiration.
- The cookie carries the user id and the user's `SecurityStamp`. On each request the stamp is checked against the database (cached briefly); a mismatch, a missing user or a disabled user signs the session out. Changing a password, a role or the disabled flag replaces the stamp, so it takes effect everywhere at once.

### 4.4 Login and brute force
- `POST /login` accepts a form post (redirects to `/`) or JSON (204).
- Every failure, whatever the cause (wrong password, unknown user, disabled user, locked name), returns the same message: "Invalid username or password."
- Per-IP limit: 10 login requests per minute (fixed window, ASP.NET Core rate limiter); over the limit, a bare 429.
- Per-username lockout: 5 consecutive failures lock that username for 15 minutes; attempts while locked return the generic failure. Counters live in memory; a restart clears them.
- Behind the proxy the server uses forwarded headers from the configured proxy only, so the limiter sees the real client IP.

### 4.5 Request forgery and errors
- SameSite=Strict cookies; non-GET API endpoints accept JSON bodies only; the login form is same-origin.
- Unexpected exceptions return a generic 500 without details; hub method failures return a generic error. Details go to the server log.
- Logs never contain passwords or hashes.

## 5. Accounts

### 5.1 User table
`Id`, `UserName` (unique, case-insensitive; 3 to 24 letters, digits, `_` or `-`), `PasswordHash` (ASP.NET Core `PasswordHasher`, PBKDF2), `IsAdmin`, `Disabled`, `SecurityStamp`, `CreatedAt`.

Password rule: at least 12 characters.

### 5.2 Endpoints
| Endpoint | Access | Purpose |
|---|---|---|
| `GET /login`, `POST /login` | anonymous | Login page and sign-in (rate-limited) |
| `POST /logout` | player | Sign out |
| `GET /api/me` | player | `{ userName, canManageUsers }` |
| `GET /api/admin/users` | admin | List users (no hashes) |
| `POST /api/admin/users` | admin | Create a user: username, password, admin yes/no |
| `PUT /api/admin/users/{id}/password` | admin | Set a password |
| `PUT /api/admin/users/{id}/role` | admin | Make admin or player |
| `PUT /api/admin/users/{id}/disabled` | admin | Disable or enable |
| `POST /api/admin/maintenance` | admin | Turn maintenance mode on or off |

Admin safety: an admin can't disable or demote themselves, and the last active admin can't be disabled or demoted.

### 5.3 First admin
At startup, if the user table is empty, the server creates an admin from `CROMOBOUND_ADMIN_USER` and `CROMOBOUND_ADMIN_PASSWORD`. If the table is empty and those are missing, the server refuses to start and logs why.

## 6. Matches

### 6.1 Hub
One SignalR hub at `/hub`, requiring the player role. Its JSON protocol uses `CromoJson.Options`, so `Deck`, `PlayerAction`, `PendingDecision` and `PlayerView` travel as the engine's own records. Each connection joins a group for its user, so all of a user's tabs receive their updates.

### 6.2 Challenges (client to server)
| Method | Behavior |
|---|---|
| `Challenge(opponent, format, deck)` | The opponent must exist, be enabled and not be in a match; the deck is checked with `DeckValidator`; one open challenge per challenger |
| `AcceptChallenge(challengeId, deck)` | The opponent's deck is checked; the match is created with `Match.Create` and a seed from a cryptographic random generator; the challenger takes seat 0 (the engine's roll-off still decides who plays first) |
| `DeclineChallenge(challengeId)`, `CancelChallenge(challengeId)` | Close the challenge |

Challenges live in memory; a restart drops open ones. While maintenance is on, `Challenge` and `AcceptChallenge` are refused with "The server is in maintenance."

### 6.3 Playing (client to server)
| Method | Behavior |
|---|---|
| `Submit(matchId, action)` | Any `PlayerAction`, including manual actions, undo and concede. The server maps the user to their seat; a match the user isn't in reads as not found. Returns `{ accepted, rejection }` with the engine's rejection code and message |
| `GetMatch()` | The user's running match id and current view, or none; used on connect and reconnect |

### 6.4 Server to client
`ChallengeReceived`, `ChallengeClosed`, `MatchStarted`, `View` (the receiving player's own `PlayerView`, after every accepted action), `MatchEnded` (game wins and winner). A player is never sent the other seat's view.

### 6.5 Rules
- One running match per player; a player in a match can't be challenged or challenge.
- No spectators, no turn timers. An admin has no extra powers inside a match.

### 6.6 Hosting and persistence
- `MatchRegistry` holds a `MatchHost` per running match: the engine `Match`, the two user ids, and a per-match lock that serializes every action.
- Match table: `Id` (GUID), `Seat0UserId`, `Seat1UserId`, `RecordJson` (the engine `MatchRecord`), `Status` (Running, Finished, Abandoned), `CreatedAt`, `UpdatedAt`.
- An accepted action, under the lock: submit to the engine, save the new record, then push each player's view. If saving fails, the host reloads the match from the last saved record and the caller gets "The action couldn't be saved, try again." A rejected action saves nothing.
- On startup every Running match is reloaded with `Match.Load`. A record the engine refuses because the engine version or card data changed (`MatchVersionMismatchException`) is marked Abandoned and logged; its players are told the match ended because the server was updated.
- A finished match is marked Finished, keeps its record (for the later match history), and leaves memory.

### 6.7 Maintenance switch
In memory, off at startup. While on, no new challenges or matches; running matches continue, so they can finish before a deploy.

## 7. Configuration
`appsettings.json` plus environment variables: database path, data folder, data-protection key folder, first-admin username and password, rate-limit numbers, known proxy addresses, cookie lifetime.

## 8. Deployment
- A multi-stage `Dockerfile` (.NET 10), running as a non-root user.
- Mounted volumes for the SQLite file and the data-protection keys, so data and sessions survive redeploys.
- HTTPS is terminated by the reverse proxy; the app trusts forwarded headers from it only.
- `docs/server-deploy.md`: VPS setup, an example Caddy config, the environment variables, backing up the SQLite file.

## 9. Testing
Server tests run the whole app in memory (`WebApplicationFactory`) with a temporary SQLite file per test, driven by an HTTP client and the SignalR .NET client. Nothing is mocked.
- **Default deny:** every endpoint, the hub and a static file reject signed-out requests; a test enumerates every mapped endpoint and fails if one besides the login endpoints allows anonymous access or lacks a role requirement.
- **No role leaks:** a player calling admin endpoints gets a bare 403 or 404 whose body and headers contain no role name or identifier.
- **Login:** identical responses for a wrong password, an unknown user and a disabled user; the per-IP limit returns 429; five failures lock a username without revealing it; a password change, demotion or disable ends sessions at once.
- **Admin:** create, set password, set role, disable; self and last-admin protection.
- **Matches:** challenge, accept, decline, cancel; invalid decks rejected; acting for the other seat or in another match refused; a full scripted Bo1 between two clients using the engine's test `Bot` ends with both receiving `MatchEnded`; a restart mid-match resumes it; a record from another engine version is marked Abandoned; no view sent to a player contains the opponent's hidden cards; maintenance blocks new challenges.

## 10. Plans
| Plan | Content |
|---|---|
| G: foundation | Server project, EF Core and SQLite, users, roles, cookie sign-in, login page, default deny, rate limiting and lockout, admin endpoints, first admin, error handling, tests |
| H: matches | Hub with CromoJson, challenges, MatchRegistry and MatchHost, persistence and restart, maintenance switch, per-player views, scripted-match tests, Dockerfile and deploy doc |

## 11. Decisions log
| Decision | Choice | Reason |
|---|---|---|
| Hosting | Linux VPS, Docker, reverse proxy, SQLite | Cheap and simple for a few friends |
| Visibility | Everything behind login except the login page | Only the owner's friends know what the site is |
| Accounts | Admin-created, permanent passwords, admin-only changes | No sign-up surface at all |
| Roles | Two, admin implies player, specific identifiers, never revealed | Defense in depth on top of signed cookies |
| Brute force | Per-IP rate limit plus per-username lockout, generic failures | Throttles guessing without revealing which names exist |
| Client | Blazor WebAssembly (Phase 4) | C# end to end; reuses the engine's records |
| Match hosting | In-memory host per match with a lock, save after every action | Simple and fast; the engine's replay makes restarts exact |
| First slice | Foundation plus live matches; no lobbies, friends, saved decks or history | Two friends can play end to end soonest |
