---
description: Create a new display panel with guided prompts for location, styling, and registration
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, AskUserQuestion
---

# Create Panel Command

Create a new display panel for LCDPossible: $ARGUMENTS

## Workflow

### Step 1: Gather Requirements

Ask the user the following questions using AskUserQuestion:

**Question 1: Panel Purpose**
- What should this panel display?
- Get a clear description of the panel's purpose

**Question 2: Plugin Location**
Ask where to create the panel:
- **Core Plugin** (Recommended) - For panels using only standard .NET libraries
- **New Plugin** - For panels requiring external NuGet packages or native dependencies
- **Existing Plugin** - Add to an existing plugin (Video, Web, Images, Proxmox)

**Question 3: Data Source**
Ask about data source:
- **System Metrics** - Uses ISystemInfoProvider (CPU, RAM, GPU data)
- **Network APIs** - Uses System.Net (network info, HTTP calls)
- **Custom Provider** - Needs a new data provider interface
- **Static/Self-contained** - Generates content without external data

**Question 4: Panel ID**
Suggest a panel ID based on purpose (lowercase-with-hyphens format).
Confirm with user.

### Step 2: Read the Skill

Read the create-panel skill for templates and patterns:
- `.claude/skills/create-panel/SKILL.md`

### Step 3: Create the Panel

Based on answers, create:

1. **Panel Class** - Using the template from the skill
   - Extend `BaseLivePanel` (or `LCDPossible.Sdk.BaseLivePanel` for plugins)
   - Implement `RenderFrameAsync` with consistent styling
   - Use appropriate layout pattern (single-column, multi-column, or graphic)

2. **Register in Plugin** - Add to appropriate plugin:
   - Add `PanelTypeInfo` to `PanelTypes` dictionary
   - Add case to `CreatePanel` switch statement

3. **If New Plugin**:
   - Create project structure
   - Create .csproj with dependencies
   - Create plugin class
   - Add to solution
   - Reference from main project

### Step 4: Update Documentation

Update `CLAUDE.md`:
- Add panel to "Available Panel Types" table

### Step 5: Build and Test

Run:
```powershell
./build.ps1
./start-app.ps1 show {panel-id}
```

### Step 6: Add to Profile (Optional)

Ask if the panel should be added to the default profile.
If yes, either:
- Update `DisplayProfile.CreateDefault()` for new installations
- Run `./start-app.ps1 profile add-panel {panel-id}` for current profile

## Output

When complete, summarize:
- Panel location and files created
- Panel ID for CLI usage: `./start-app.ps1 show {panel-id}`
- How to add to profiles: `./start-app.ps1 profile add-panel {panel-id}`
