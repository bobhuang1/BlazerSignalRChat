# BlazerSignalRChat

Messenger-style chat demo built with **Blazor Server** and **SignalR**. Open a
few browser tabs (each tab acts as its own demo user) and chat in real time —
messages, typing indicators, and online presence all fan out over SignalR.

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![server](https://img.shields.io/badge/Blazor-Server-512BD4) ![signalr](https://img.shields.io/badge/SignalR-%E2%9A%A1-0084ff)

## How to run

```bash
dotnet run
```

Open `https://localhost:<port>` (or `http://localhost:<port>`). To see live
chat, open **two or more tabs**.

## Demo users

Pick a user from the top-right switcher. The choice is remembered per tab
(`sessionStorage`), so each tab can be a different person.

| User  | Hue  | Bio                     |
|-------|------|-------------------------|
| Alice | pink | Usually online mid-morning |
| Bob   | blue | Loves board games and coffee |
| Carol | green| Frontend enthusiast     |
| Dave  | orange | Runs the demo servers  |
| Eve   | purple | Here to test SignalR   |

## Chat lists (Messenger-style left panel)

Three panels:

- **Chats** — 3 group rooms (General, Announcements, Random) with last-message
  previews, unread badges and live member counts. Filter with the chips above
  the list (All / Unread / Groups / Communities) and search across names and
  previews with the search bar.
- **Friends** — all 5 demo users with online/last-seen presence.
- **Archived** — archive a chat with the 📦 button in its header; restore it
  with ↺.

Group rooms:

| Room             | Kind       | Purpose       |
|------------------|------------|---------------|
| General          | Group      | Chat with everyone |
| Announcements    | Group      | Team updates  |
| Random           | Community  | Off-topic fun |

The right-hand **Members** pane lists the profiles of everyone in the active
chat with their bio, online/last-seen status, and live presence dots (collapses
on narrow windows).

## Features

- Real-time messaging over SignalR (`/chat` hub) with an in-memory history
  store (capped at 500 messages per room).
- Per-circuit identity via `SessionState` — every tab is its own user, perfect
  for testing.
- Typing indicators that self-expire after 4 seconds.
- Online presence + per-room online counts broadcast on connect/disconnect.
- System feed ("Alice joined / left") inside the chat.
- Automatic reconnect with a status pill in the top bar.
- Responsive layout (sidebar collapses above the feed on narrow screens).

## Project layout

```
BlazerSignalRChat/
  Chat/
    ChatModels.cs    ChatUser / ChatRoom / ChatMessage / RoomSnapshot
    ChatState.cs     in-memory users, rooms, history, presence, typing
    ChatHub.cs       SignalR hub (join/leave/send/typing/presence)
  Services/
    SessionState.cs  per-tab identity (scoped per circuit)
  Components/
    Pages/Home.razor the messenger UI (sidebar panels + chat column)
  wwwroot/
    css/app.css      messenger theme
    js/chat.js       scroll-to-bottom + input focus helpers
```

## Testing tips

Need 5 users at once? Open 5 tabs and pick Alice, Bob, Carol, Dave and Eve.
Watch the Friends panel switch to "online" and the room member count climb as
each tab opens. Type in one tab and the others see "… is typing" live.

Note: all state is in-memory and resets when the app restarts — intentional for
a demo.