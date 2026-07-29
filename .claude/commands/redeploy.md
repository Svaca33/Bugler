---
description: Rebuild and restart the bugler container so manual testing hits the current code
---

Redeploy Bugler so the user can click through the change on the build that actually ships.

1. If you started the frontend dev server through the preview tooling in this session, stop it
   with `preview_stop` first, so the preview pane does not drift from what is really running.
2. Run the script with a generous timeout - the image build compiles the backend and the
   frontend from scratch and can take several minutes:

   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/redeploy.ps1
   ```

3. On success, tell the user the new version is up at http://localhost:8080 and name what
   changed, so they know what to look at. Do **not** commit - they test manually first.
4. On failure, report which step failed (gate, image build, or health) and show the relevant
   part of the output. Do not claim the redeploy succeeded.

postgres keeps running throughout, so the admin account and the ingested telemetry survive.
