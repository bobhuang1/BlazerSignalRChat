# BlazerSignalRChat — Messenger.com-style chat demo (Blazor Server + SignalR)

A faithful clone of the **Messenger.com** web UI built with **Blazor Server**
and **SignalR**: three-column layout — top navigation, inbox with filter chips,
conversation with bubbles/date separators/typing indicator, and a profile pane
with chat info, member presence, and customizable colors.

Every browser tab is its own demo user (`sessionStorage` identity), so opening
a few tabs gives a live multi-user chat with no database and no login — all
state is in-memory and resets on restart.

## Features

**Layout**

- Top navigation — logo, home/people/video/store/games tabs, search, tabular
  notification badge, per-tab user switcher.
- Inbox (left) — search bar, filter chips **All / Unread / Groups /
  Communities**, room rows with last-message previews, timestamps and unread
  dots; Friends (👥) and Archived (📦) panels toggled from the header.
- Conversation (center) — `M/d/yy, h:mm tt` date separators, gray incoming /
  blue outgoing bubbles, reaction pills on the bubble corner, typing
  indicator, composer with quick media icons.
- Profile pane (right) — room hero, Profile/Mute/Search buttons, accordion
  sections (chat info + member list, chat colors, media, privacy); hideable
  with the ⓘ button, auto-collapses on narrow windows.

**Realtime**

- Messaging over the `/chat` SignalR hub, in-memory history capped at 500
  messages per room.
- Emoji picker — 8 categories with keyword search, inserts at the caret.
- Message reactions — hover a message for ❤️ 👍 😂 😮 😢 😡; pills sync live
  to everyone in the room, including clients that join later; clicking your
  reaction again removes it.
- 👍 quick-like button (sends a like when the input is empty); Enter or ▲ to
  send text.
- Typing indicators (4-second self-expiry), online presence with per-room
  counts, system feed ("Alice joined / left").
- Per-room unread badges that survive switching rooms (all tabs join every
  room at startup), auto-reconnect with a status pill.

## Demo users

| User  | Hue    | Bio                       |
|-------|--------|---------------------------|
| Alice | pink   | Usually online mid-morning|
| Bob   | blue   | Loves board games and coffee |
| Carol | green  | Frontend enthusiast       |
| Dave  | orange | Runs the demo servers     |
| Eve   | purple | Here to test SignalR      |

## Rooms

| Room          | Kind      | Purpose            |
|---------------|-----------|--------------------|
| General       | Group     | Chat with everyone |
| Announcements | Group     | Team updates       |
| Random        | Community | Off-topic fun      |

## How to run

```bash
dotnet run
```

Open `http://localhost:<port>`, then open **two or more tabs** — each tab
picks its own user from the top-right switcher (`sessionStorage`, remembered
per tab). Type in one tab and watch messages, typing, presence and reactions
fan out live in the others.

## Project layout

```
BlazerSignalRChat/
├── Chat/
│   ├── ChatModels.cs     ChatUser / ChatRoom / ChatMessage / RoomSnapshot
│   ├── ChatState.cs      in-memory users, rooms, history, presence, reactions
│   └── ChatHub.cs        SignalR hub (join/leave/send/typing/presence/react)
├── Services/
│   └── SessionState.cs   per-tab identity (scoped per circuit)
├── Components/
│   └── Pages/Home.razor  the Messenger UI (topnav + inbox + chat + profile)
└── wwwroot/
    ├── app.css           Messenger.com theme (incl. emoji picker, reactions)
    └── js/chat.js        scroll / focus / popover-close helpers
```

## Testing tips

Open 5 tabs and pick Alice, Bob, Carol, Dave and Eve. Watch the Friends panel
switch to "online" and the room member counts climb as each tab connects.
React to a message in one tab and the pill appears in all of them.
