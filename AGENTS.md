# AGENTS.md

## Content exclusion

- If a file is indicated as excluded from Copilot context (for example, a read attempt reports that the file is ignored or excluded), stop immediately.
- A workspace hook may also deny tool calls that target excluded files; treat that as expected policy enforcement, not as an obstacle to bypass.
- Do **not** attempt to inspect, read, summarize, search, or otherwise recover the contents of that file through alternative tools or indirect workarounds.
- This rule applies even if the user explicitly asks to read the excluded file.
- Instead, explain that the file is protected by content exclusion settings and ask the user to provide non-excluded input or remove the exclusion if they want help with that content.
- Do not suggest or perform bypasses of the exclusion rule.
