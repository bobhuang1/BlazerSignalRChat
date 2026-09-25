# BlazerSignalRChat

A faithful **Messenger.com-style chat demo** built with **Blazor Server** and
**SignalR**. Three-column layout — top navigation, inbox, conversation, profile
pane. Open a few browser tabs (each tab acts as its own demo user) and chat in
real time — messages, typing indicators, presence, emoji and reactions all fan
out over SignalR.

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![server](https://img.shields.io/badge/Blazor-Server-512BD4) ![signalr](https://img.shields.io/badge/SignalR-%E2%9A%A1-0084ff)

## How to run

```bash
dotnet run
```

Open `https://localhost:<port>` (or `http://localhost:<port>`). To see live
chat, open **two or more tabs**.

## Layout

- **Top navigation** — logo, home/people/video/store/games tabs, search,
  notifications badge and the per-tab user switcher.
- **Inbox (left)** — "Chats" panel with search, filter chips
  (**All / Unread / Groups / Communities**), room rows with last-message
  previews, timestamps and unread dots, plus **Friends** (👥) and
  **Archived** (📦) panels toggled from the header.
- **Conversation (center)** — date separators, gray/blue bubbles, typing
  indicator, and a composer with quick media icons, emoji pill, and 👍 like
  button (press Enter or the send arrow ▲ to send).
- **Profile pane (right)** — room hero, profile/mute/search buttons and
  accordion sections for chat info (member list with presence), **customizable
  chat colors**, media, and privacy. Hide it with the ⓘ header button or on
  narrow windows.

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

## Rooms

| Room             | Kind       | Purpose       |
|------------------|------------|---------------|
| General          | Group      | Chat with everyone |
| Announcements    | Group      | Team updates  |
| Random           | Community  | Off-topic fun |

## Features

- Real-time messaging over SignalR (`/chat` hub) with an in-memory history
  store (capped at 500 messages per room).
- **Emoji picker** — 8 categories with keyword search; tap the 😊 pill in the
  composer.
- **Message reactions** — hover a message and pick ❤️ 👍 😂 😮 😢 😡.
  Reactions sync live to everyone in the room (including clients that join
  later); clicking your reaction again removes it.
- **👍 like button** — sends a like instantly when the input is empty.
- Per-circuit identity via `SessionState` — every tab is its own user, perfect
  for testing.
- Typing indicators that self-expire after 4 seconds.
- Online presence + per-room online counts broadcast on connect/disconnect.
- Unread badges per room (cross-room: joining all rooms at startup) and system
  feed ("Alice joined / left").
- Chat color themes (per-room accent swatches in Customize chat).
- Automatic reconnect with a status pill in the top bar.
- Responsive: profile pane collapses below 1240px, nav tabs below 980px,
  stacked inbox/conversation below 820px.

## Project layout

```
BlazerSignalRChat/
  Chat/
    ChatModels.cs    ChatUser / ChatRoom / ChatMessage / RoomSnapshot
    ChatState.cs     in-memory users, rooms, history, presence, reactions
    ChatHub.cs       SignalR hub (join/leave/send/typing/presence/react)
  Services/
    SessionState.cs  per-tab identity (scoped per circuit)
  Components/
    Pages/Home.razor the Messenger UI (topnav + inbox + chat + profile)
  wwwroot/
    app.css          Messenger.com theme
    js/chat.js       scroll/focus/popover-close helpers
```

## Testing tips

Need 5 users at once? Open 5 tabs and pick Alice, Bob, Carol, Dave and Eve.
Watch the Friends panel switch to "online" and the room member count climb as
each tab opens. Type in one tab and the others see "… is typing" live. React
to a message in one tab and the pill appears in all of them.

Note: all state is in-memory and resets when the app restarts — intentional for
a demo.
