name: Bug Report
description: Report a problem with the mod
title: "[Bug] "
labels: bug
assignees: ''

body:
  - type: textarea
    attributes:
      label: What happened?
      description: Describe the bug and what you expected to happen.
    validations:
      required: true

  - type: textarea
    attributes:
      label: Steps to reproduce
      description: Tell me how to trigger the bug step by step.
    validations:
      required: true

  - type: input
    attributes:
      label: Mod version
      description: What version of HealOnLobby are you using?
    validations:
      required: true

  - type: textarea
    attributes:
      label: Additional context
      description: Any logs, screenshots, or notes that might help.
    validations:
      required: false
