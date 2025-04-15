name: Bug Report
description: Report a problem with the mod
title: "[Bug] "
labels: bug
assignees: ""

body:
  - type: markdown
    attributes:
      value: |
        ### 🐞 **Describe the Bug**  
        → Clearly explain what went wrong and what you expected instead.  
        *(Example: "Health doesn't restore in the lobby after taking damage on the server.")*

  - type: textarea
    attributes:
      label: "**📌 What happened?**"
      placeholder: |
        - [ ] Expected: Health should restore to 100% in lobby.
        - [ ] Actual: Health stays at damaged level.
    validations:
      required: true

  - type: textarea
    attributes:
      label: "**🛠 Steps to Reproduce**"
      placeholder: |
        1. Join a server.
        2. Take damage (e.g., fall from height).
        3. Return to the lobby.
        4. Observe health bar.
    validations:
      required: true

  - type: input
    attributes:
      label: "**ℹ️ Mod Version**"
      placeholder: "e.g., 1.2.0"
    validations:
      required: true
