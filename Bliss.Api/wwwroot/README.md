Frontend source no longer lives here.

Use the repository `frontend/` folder:

- `frontend/public` — public advertiser/creator experience
- `frontend/operations` — operator console

`Bliss.Api` still needs this `wwwroot` directory because ASP.NET Core
expects it at host startup. Keep it empty of application UI files.
