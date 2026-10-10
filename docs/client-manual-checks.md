# Client manual checks (Phase 4a)

Run these against a local server (`dotnet run --project src/CromoBound.Server`) before deploying a client change. The automated tests
cover each page against fakes; these checks cover the real browser, the real hub and the real cookie.

You need two accounts (an admin and a player) and two browsers, or one normal and one private window.

## Signing in
- [ ] Opening `/` signed out shows only the login page; `/decks`, `/match/<any id>` and `/_framework/blazor.webassembly.js` do too.
- [ ] After signing in, the top bar shows the logo, Play, Decks, the username and Sign out; the admin also sees Admin.
- [ ] Sign out goes to the login page, and the back button doesn't show the app again.

## Decks
- [ ] Pasting a deck JSON from `schema/deck.schema.json`'s format adds a deck tile with its counts.
- [ ] Pasting `{` shows "That isn't a deck the app can read: it isn't valid JSON." and adds nothing.
- [ ] Uploading a `.json` file fills the box; Add deck adds it.
- [ ] Rename and Delete work, and survive a reload.
- [ ] Signed in as the other account in the same browser, the decks list is empty.

## Lobby
- [ ] Each account sees the other, Online, with a green dot; closing the other browser turns it Offline.
- [ ] Challenging with a deck that isn't legal shows the server's problems; Pick another deck reopens the challenge.
- [ ] A legal challenge shows under Sent; the other browser pops up "<name> challenges you to a best of ..." and shows it under Received.
- [ ] Cancel challenge clears both sides, and the other browser pops up "<name> cancelled their challenge."
- [ ] Decline clears both sides, and the challenger pops up "<name> declined your challenge."
- [ ] Accept and play opens the match page in both browsers.

## Match
- [ ] The page shows "<you> vs <them>", the format and game, the stage and the score.
- [ ] Reloading the page shows the same.
- [ ] Concede asks first; Keep playing does nothing; Concede in a best of one shows the result dialog in both browsers, and "Back to the lobby" goes to the lobby.
- [ ] Opening `/match/<another id>` goes to the lobby.

## Connection and session
- [ ] Stopping the server shows "Reconnecting..." and disables Challenge and Accept; starting it again clears the banner and the lobby is current.
- [ ] Disabling the player's account from the admin's browser sends the player's browser to the login page.
- [ ] Restarting the server during a match shows "Your match was stopped" once, in the lobby.

## Admin
- [ ] Users lists every account with role and account state; creating one with a short password shows the server's message in the dialog.
- [ ] Make admin, Remove admin, Disable and Enable update the row; disabling your own account pops up "You can't do that to your own account."
- [ ] Maintenance mode shows the blue banner to every player and disables Challenge and Accept; the running-matches count drops as matches end.
- [ ] A player who opens `/admin/users` and `/admin/maintenance` sees only "You can't do that.", with no admin tabs.
- [ ] The same player's direct calls to `/api/admin/users` are refused (403), and the answer names no role.

## Release build
- [ ] `dotnet publish src/CromoBound.Server -c Release` has 0 warnings, and the published app passes "Signing in" and "Lobby" above.
