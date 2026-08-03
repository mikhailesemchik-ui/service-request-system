# Screenshot capture checklist

Viewport: **1440 × 1000**. No devtools, no browser chrome visible. No credentials in frame.
Start the API and Angular client using the instructions in README.md before capturing.

## Files to capture

| Filename | Role | URL | What to show |
|---|---|---|---|
| `login.png` | — | `/login` | Login form, clean empty state |
| `dashboard-admin.png` | admin | `/dashboard` | All stats cards visible |
| `dashboard-employee.png` | employee | `/dashboard` | Employee-scoped stats |
| `requests-list.png` | agent | `/requests` | List with several requests, status filter open |
| `request-details.png` | admin | `/requests/{id}` | Request #1 (InProgress, assigned), management controls visible |
| `comments-history.png` | agent | `/requests/{id}` | Scrolled to comments section, internal note visible |

## Tips

- Log out between sessions to avoid stale UI state.
- Use the seeded request data; do not add extra test data before capturing.
- Ensure the sidebar navigation shows the correct active link for each screenshot.
- For `request-details.png`, use a request assigned to "Development Support Agent" so the
  assignment select is visible to admin.
- For `comments-history.png`, request #8 (WaitingForUser) has both a public comment and an
  internal note.
