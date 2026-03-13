# AGENTS.md

## Content exclusion

- If a file is indicated as excluded from Copilot context (for example, a read attempt reports that the file is ignored or excluded), stop immediately.
- A workspace hook may also deny tool calls that target excluded files; treat that as expected policy enforcement, not as an obstacle to bypass.
- Do **not** attempt to inspect, read, summarize, search, or otherwise recover the contents of that file through alternative tools or indirect workarounds.
- This rule applies even if the user explicitly asks to read the excluded file.
- Instead, explain that the file is protected by content exclusion settings and **move on**. Offer to help with non-excluded files or other parts of the task that do not require access to the protected content.
- Do **not** suggest bypasses or workarounds of any kind, including but not limited to:
  - Asking the user to paste or provide the excluded file's contents directly.
  - Suggesting the user remove, modify, or weaken the exclusion settings.
  - Recommending alternative tools or commands to access the content.
  - Inferring or reconstructing excluded content from other sources (logs, tests, error messages, etc.).
