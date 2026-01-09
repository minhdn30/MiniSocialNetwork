# 🧑‍🤝‍🧑 Mini Social Network

A mini social network project built with **ASP.NET Core 8 (API)** and **ReactJS** for frontend.

---

## 🚀 Features
- User authentication (email/password, Google, Facebook)
- Realtime chat (private & group)
- Posts with images & videos
- Stories with privacy settings
- Follow and unfollow users
- Group management (admin, moderator, members)
- Blocking and reporting system

---

## 🛠️ Tech Stack
- **Backend:** ASP.NET Core Web API, Entity Framework Core
- **Frontend:** HTML, CSS, JS
- **Authentication:** JWT + OAuth2 (Google/Facebook)
- **Database:** PostgreSQL
- **Realtime:** SignalR

---

## ⚙️ How to Run (Backend)
```bash
cd SocialNetwork.API
dotnet restore
dotnet ef database update
dotnet run
