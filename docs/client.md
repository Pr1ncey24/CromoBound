# CromoBound: Client Design (Phase 4a, app shell and lobby)

Status: approved design, before implementation. Builds on the server (`docs/server.md`) and the engine (`docs/engine-architecture.md`).

## 1. Goal and scope

Phase 4 is the web UI, in three parts, each with its own spec, plans and build:

| Part | Content |
|---|---|
| **4a: app shell and lobby (this spec)** | The Blazor WebAssembly app behind login, background session renewal, the hub connection, the lobby with presence and challenges, decks kept in the browser, a placeholder match page, admin pages |
| 4b: play screen | The board from the player's `PlayerView`, a control for every decision, manual actions, the event log, undo and concede |
| 4c: cards and decks | Card browser, deck builder with live `DeckValidator`, decks saved on the server |

### In scope (4a)
- A Blazor WebAssembly app served by the server, only to signed-in players.
- A shared contracts project for the hub and HTTP records.
- Sessions kept alive in the background while the app is open.
- A hub connection that reconnects by itself and restores the lobby.
- A lobby that lists the players with their presence, lets players challenge, accept, decline and cancel, and shows your own open challenges after a reconnect.
- Decks pasted or uploaded as JSON and kept in the browser.
- A placeholder match page with concede and the result.
- Admin pages for users and maintenance.
- Server tests, and client logic and component tests.

### Out of scope
- The board (4b).
- The card browser, the deck builder, and decks saved on the server (4c).
- Friends, chat, spectators.
- Phone and tablet layouts.
- Browser automation tests.

### Success criteria
- Signed out, nothing of the app can be downloaded. The login page is still the only public thing.
- Two friends sign in, see each other online, and start a match with pasted decks. Both land on the match page, and either can concede.
- A tab left open never needs a new sign-in. If the connection drops, it comes back without user action, with the lobby restored, own open challenge included.
- An admin manages users and maintenance from the app.

## 2. Decisions summary

| Topic | Decision |
|---|---|
| Order of Phase 4 | 4a shell and lobby, then 4b play screen, then 4c cards and decks |
| Hosting | A Blazor WebAssembly project hosted by the server; its files are endpoints behind the player role |
| Component library | MudBlazor, default theme following the system's light or dark mode |
| Devices | Desktop and laptop browsers; other screens work but aren't designed for |
| Finding players | A player list with presence (online, in a match), not typed usernames |
| Decks in 4a | Pasted or uploaded JSON, kept in the browser per username |
| Session lifetime | Sliding only; a session in use never expires |
| Session renewal | The app renews the session in the background while it is open |
| Language | English, like the card text |

## 3. Projects and layout

```
src/CromoBound.Contracts/      hub replies and notices, IGameClient, HTTP records, wire JSON settings
src/CromoBound.Client/         the Blazor WebAssembly app
  Program.cs                   services: HttpClient, session keeper, game connection, deck store, MudBlazor
  Layout/                      app bar, navigation, banners (reconnecting, maintenance)
  Pages/                       Lobby, Decks, Match, Admin/Users, Admin/Maintenance
  Services/                    SessionKeeper, GameConnection, DeckStore, ServerApi
  wwwroot/index.html           the app's page
tests/CromoBound.Client.Tests/ xUnit + bUnit
```

- **`CromoBound.Contracts`** is a class library with no packages, referenced by the server and the client. It references `CromoBound.Engine`, `CromoBound.Data` and `CromoBound.Models`, because the records carry engine and data types (`PlayerView`, `PlayerAction`, `Rejection`, `MatchFormat`, `DeckIssue`). It holds:
  - the hub records and `IGameClient` (they move from `CromoBound.Server.Hubs`);
  - the HTTP records: login, `/api/me`, admin users, maintenance (they move from `CromoBound.Server.Accounts`);
  - the wire JSON settings (from `CromoBound.Server.ServerJson`, see §10).
- **`CromoBound.Client`** uses the Blazor WebAssembly SDK. Its packages are the WebAssembly runtime, the SignalR client and MudBlazor, and it references `CromoBound.Contracts`.
- **`CromoBound.Server`** references the client project and the WebAssembly server package, so it can serve the app.
- **`CromoBound.Client.Tests`** adds bUnit as its only new package. The engine, models and data projects keep their no-package rule.

## 4. Serving the app

- The server maps the app's files as static asset endpoints. They fall under the fallback policy, so every file of the app needs a signed-in player. A signed-out request for one gets a bare 401.
- `/` and the app's routes (`/decks`, `/match/{id}`, `/admin/users`, `/admin/maintenance`) serve the app's `index.html`, behind the player role. Signed out, a page request is redirected to `/login`, as today.
- The placeholder home page (`Pages.Home`) is removed. The login page stays server-rendered, as it is, and still redirects to `/` after sign-in.
- The admin pages are hidden from players in the UI only. Every admin endpoint keeps its admin policy, which is the real protection.
- `DefaultDenyTests` keeps enumerating every endpoint, the app's files included, and still allows anonymous access only to `/login`.
- After sign-in the browser downloads the .NET runtime, the app, and the engine and contracts code (a few MB; cached after the first visit). No card data is downloaded in 4a.

## 5. Sessions and the connection

### 5.1 Background renewal
- While the app is open, `SessionKeeper` calls `GET /api/me` every 30 minutes.
- The cookie's sliding expiration renews it once more than half of its lifetime (`CookieHours`, default 12) has passed. So a tab left open keeps its session indefinitely, while a session nobody uses still lapses after `CookieHours`.
- A 401 from the ping or from any API call means the session ended: the account was disabled, the password or role changed, or the computer slept past the lifetime. The app then navigates to `/login`.

### 5.2 The hub connection
- `GameConnection` keeps one hub connection per tab, with automatic reconnect: retries after 0, 2, 5 and 10 seconds, then every 30 seconds for as long as the tab is open.
- While it is down, a "Reconnecting..." banner shows and every action that needs the hub is disabled.
- After every connect and reconnect, the app calls `GetLobby()` (§6.1) and replaces its whole lobby state with the answer.
- The hub closes itself when the session's original lifetime ends (`CloseOnAuthenticationExpiration`). With the renewed cookie the reconnect succeeds, and the user only sees a brief banner.
- When the server closes the connection (an account change, or the session filter), the app pings `/api/me`: a 401 goes to `/login`; otherwise it reconnects.
- Every tab is its own connection and receives every notice, as the server's user groups already do.

## 6. Lobby and presence

### 6.1 Server: `GetLobby()`
A new hub method, answered from the server's current state:

```
LobbyReply(
  Players:      every enabled user except the caller: PlayerPresence(UserName, Online, InMatch), sorted by name
  Challenges:   the caller's open challenges, made and received: ChallengeInfo(ChallengeId, From, To, Format)
  MatchId:      the caller's running match, or null
  Ended:        once, the notice of a match of the caller's abandoned at startup (MatchEndedNotice), or null
  Maintenance:  whether maintenance is on)
```

- `Online` means the user has at least one open hub connection.
- The one-time abandoned notice moves here from `GetMatch`, so the lobby shows it. `GetMatch` keeps returning the running match and the caller's view, for 4b, without `Ended`.

### 6.2 Server: presence notices
Sent to every connected player:

| Notice | When |
|---|---|
| `PlayerChanged(PlayerPresence)` | A user's first connection opens or last one closes; a match starts or ends for them; an admin creates or re-enables them |
| `PlayerLeft(UserName)` | An admin disables a user |
| `MaintenanceChanged(On)` | An admin switches maintenance |

A connected player isn't sent their own `PlayerChanged`. The existing notices stay as they are: `ChallengeReceived`, `ChallengeClosed`, `MatchStarted`, `View` and `MatchEnded`.

### 6.3 The lobby page (`/`)
- **Players table:** each player's name and status chips (online or offline, in a match), with a **Challenge** button.
  - An offline player can be challenged; they see it when they next connect.
  - The button is disabled while you or they are in a match, or while you already have an open challenge.
- **Challenge dialog:** pick Bo1 or Bo3 and one of your decks. A refused deck shows the server's list of problems.
- **Challenges panel:**
  - challenges you received, with **Accept** (pick your deck) and **Decline**;
  - your sent challenge, with **Cancel**.
  - A new or closed challenge also pops up as a short notification.
- **Maintenance banner** while maintenance is on. Challenging and accepting are disabled.
- **Abandoned notice:** when `GetLobby` carries one, a dialog says the match ended because the server was updated.
- **Going to the match:** when a match starts, or when `GetLobby` reports a running match, the app goes to its page.

## 7. Decks (`/decks`)
- **Adding:** paste JSON or upload a `.json` file, in the `Deck` format of `CromoBound.Models` (`CromoJson`). The deck's `name` field is its name, and you can rename or delete it.
- **Storage:** decks live in the browser's local storage under a key that includes the username, so two accounts on one computer don't mix. Another computer starts empty.
- **Checks:** the app only checks that the JSON reads as a `Deck`. Legality is the server's, at challenge and accept time.
- 4c replaces this with decks saved on the server.

## 8. The match page (`/match/{id}`, placeholder until 4b)
- It shows the opponent, the format, the stage and the game wins, from `GetMatch` and the `View` notices.
- **Concede** asks for confirmation, then submits `Concede`.
- On `MatchEnded`, a result dialog names the winner and offers the way back to the lobby.
- A match id that isn't the player's running match sends them back to the lobby.

## 9. Admin pages
These show only when `/api/me` reports `canManageUsers`.

- **Users (`/admin/users`):** a table of users (name, admin, disabled), with dialogs to:
  - create a user;
  - set a password;
  - make or remove an admin;
  - disable or enable a user.

  The server's refusals are shown as they come ("You can't do that to your own account.", "The last admin can't be removed.").
- **Maintenance (`/admin/maintenance`):** the switch and the running-matches count, refreshed on `MaintenanceChanged` and on `PlayerChanged`, so a deploy can wait for zero.
- **App bar:** the username, links to Lobby, Decks and (for admins) Admin, and **Sign out**, which posts to `/logout` and goes to `/login`.

## 10. Wire JSON
- The hub and the saved match records use the engine's JSON settings without indentation, as in Phase 3, plus one change: empty lists are written as `[]` instead of being left out.
- This way a view the client reads back is exactly the view the engine built. Before, empty lists read back as null.
- Records saved the old way still load, because a missing list reads as empty where the engine's records have defaults, and saved records already round-trip.

## 11. Errors
- An HTTP refusal shows the server's message in a notification.
- A 401 navigates to `/login`.
- A 403 shows "You can't do that."
- A hub reply's error is shown as it comes. The hub's generic error shows "Something went wrong."
- Blazor's unhandled-error bar reads "Something went wrong. Reload the page." and never shows details.

## 12. Testing
- **Server:**
  - every app file and route needs sign-in (files get a 401 and pages redirect when signed out), and `index.html` is served to a signed-in player;
  - `GetLobby` contents, including your own sent challenge after a reconnect;
  - `PlayerChanged` on first connect, last disconnect, match start and end, and creating or re-enabling a user;
  - `PlayerLeft` on disable, and `MaintenanceChanged`;
  - the abandoned notice comes from `GetLobby` once, and no longer from `GetMatch`;
  - views and records round-trip exactly with empty lists;
  - `DefaultDenyTests` covers everything new.
- **Client (`CromoBound.Client.Tests`):**
  - xUnit tests for `DeckStore` (parse, name, per-user keys), `SessionKeeper` (ping schedule, 401 handling, with a fake clock) and `GameConnection`'s state handling;
  - bUnit tests for the lobby, decks, match and admin pages against fake connection and API services.
- **Manual check list** in the plan: sign-in, two browsers playing to a concede, a server restart mid-lobby, and admin changes, all against a locally running server.

## 13. Plans
| Plan | Content |
|---|---|
| I: server side of 4a | The contracts project and the records' move, wire JSON keeping empty lists, hosting the app behind the player role, `GetLobby`, presence and maintenance notices, `GetMatch` without the abandoned notice, server tests |
| J: the client | The Blazor WebAssembly app: shell, session keeper, game connection, lobby, decks, match placeholder, admin pages, client tests, manual check list |

Before Plan J is written, every screen (lobby with its dialogs, decks, match placeholder, admin users and maintenance, the banners) is mocked as an artifact and reviewed by the owner. The plan builds what the approved mocks show.

## 14. Decisions log
| Decision | Choice | Reason |
|---|---|---|
| Phase 4 order | Shell, then play screen, then decks | Friends can play soonest; the play screen is the largest and riskiest part |
| Hosting | Server-hosted WebAssembly, files behind the player role | Keeps the private-site rule for the app itself; same origin keeps the SameSite=Strict session |
| Shared records | A `CromoBound.Contracts` project | One definition for the server and the client |
| UI library | MudBlazor | Dialogs, tables, forms and notifications ready-made, for the shell now and the board later |
| Devices | Desktop and laptop | The board needs a large screen |
| Presence | Player list with online and in-match status | Friends see who's around and challenge with a click |
| Decks before 4c | Pasted JSON in the browser | No server work that 4c would replace |
| Renewal | `/api/me` ping every 30 minutes while open | A session in use never expires; hub traffic alone doesn't renew the cookie |
| Reconnect | Automatic, then `GetLobby` replaces the state | One call restores everything, own open challenges included |
| Empty lists on the wire | Written as `[]` | Views read back exactly, which 4b relies on |
