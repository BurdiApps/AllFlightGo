# Render Deployment Setup

How AllFlightGo is deployed to Render, and how to fix the two things that most
commonly break the `/admin` area on the live site.

**Live site:** https://allflightgo.onrender.com

---

## How it deploys

Render builds the `Dockerfile` in this repo and runs the container. When the
GitHub repo is connected with auto-deploy on, every push to `main` triggers a new
build (~4–8 minutes).

- **Repo:** `BurdiApps/AllFlightGo`
- **Branch:** `main`
- **Port:** the container listens on `8080` (`ASPNETCORE_URLS=http://+:8080`).

---

## Required environment variables

The live server runs in **Production**, so `appsettings.Development.json` is **not**
loaded. Any config that lives there for local dev must be set as environment
variables on Render instead.

.NET maps a config key like `Admin:Email` to an environment variable by replacing
the `:` with a **double underscore** → `Admin__Email`. Get this wrong and the
setting is ignored.

| Environment variable | Value | Why it's needed |
|----------------------|-------|-----------------|
| `Admin__Email` | `afgadmin@email.com` | The admin account is **seeded from config at startup**. Without this, no admin user is created and `/admin/login` rejects every attempt. |
| `Admin__Password` | `Admin123!` | Same seed. Must satisfy Identity's password rules (uppercase, lowercase, digit, symbol) or the seed silently fails. |
| `ConnectionStrings__DefaultConnection` | `Data Source=/var/data/allflight.db` | *(Optional)* Points SQLite at a persistent disk so data survives redeploys. Omit and it defaults to a temporary `allflight.db` inside the container. |

Also set (or confirm) any secrets used for the main app:

| Environment variable | Notes |
|----------------------|-------|
| Duffel API key | Whatever key name `DuffelService` reads. |
| Google OAuth client ID / secret | For the Google sign-in flow. |

---

## Fixing the `/admin` 404 (repo was renamed)

The repo was renamed from `AllFlight` to **`AllFlightGo`**, which can disconnect
Render's auto-deploy webhook and leave it serving an old build (symptom:
`/admin/login` returns **404** while the homepage still works).

**Fix, in the Render dashboard:**

1. Open the **AllFlightGo** web service → **Settings**.
2. Under **Repository**, confirm it points to **`BurdiApps/AllFlightGo`**
   (reconnect it if it still shows the old `AllFlight` name).
3. Confirm **Branch** is **`main`**.
4. Go to **Environment** and add the variables from the table above
   (`Admin__Email`, `Admin__Password`). Save.
5. Top-right → **Manual Deploy → Deploy latest commit**.
6. Wait ~5 minutes, then open
   [`/admin/login`](https://allflightgo.onrender.com/admin/login). It should load
   the login page, and `afgadmin@email.com` / `Admin123!` should sign in.

---

## Heads up: the database is temporary

By default the app uses a SQLite file (`allflight.db`) inside the container. On
Render that file is **ephemeral** — it resets on every deploy and whenever a free
instance spins down.

- The **admin account re-seeds automatically** on each restart, so admin login
  keeps working.
- But any **flights or bookings added at runtime are wiped** on redeploy.

For a class demo this is usually fine (just re-add a couple flights after a
deploy). To make data persist, attach a **Render persistent disk** (e.g. mounted
at `/var/data`) and set `ConnectionStrings__DefaultConnection` to
`Data Source=/var/data/allflight.db` as shown above.

---

## Free-tier note

On Render's free tier the service **spins down after ~15 minutes idle**, so the
first visit after that takes an extra ~30–60 seconds to wake up. That's normal
and separate from deploys.
</content>
