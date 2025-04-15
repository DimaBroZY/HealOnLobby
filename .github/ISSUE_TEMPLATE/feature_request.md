name: Feature Request
description: Suggest an idea for the mod
title: "[Feature] "
labels: enhancement
assignees: ""

body:
  - type: markdown
    attributes:
      value: |
        ### ✨ **Feature Idea**  
        → What should the mod do? Be specific!  
        *(Example: "Add a command /heal to manually restore health in the lobby.")*

  - type: textarea
    attributes:
      label: "**📌 What's the idea?**"
      placeholder: |
        - [ ] New command: `/heal [player]`  
        - [ ] Config option to toggle auto-heal  
    validations:
      required: true

  - type: textarea
    attributes:
      label: "**❓ Why is it useful?**"
      placeholder: |
        - Fixes cases where auto-heal fails.  
        - Lets admins control healing manually.  
    validations:
      required: false

  - type: textarea
    attributes:
      label: "**🎨 Mockups/Examples**"
      description: "Link images or describe how it should look."
      placeholder: |
        - **Command example**: `/heal @DimaBroZY`  
        - **Config example**: `autoHeal: true/false`  
