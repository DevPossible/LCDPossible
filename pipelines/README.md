# Azure DevOps Pipelines

This directory contains Azure DevOps pipeline definitions for LCDPossible.

## Pipelines

| Pipeline | File | Trigger | Purpose |
|----------|------|---------|---------|
| CI | `ci.yml` | PRs to main/develop, push to develop | Build, test, and lint |
| Release | `release.yml` | Push to main | Build artifacts and create GitHub release |

## Setup Requirements

### 1. Variable Group

Create a variable group named `lcdpossible-secrets` with the following variables:

| Variable | Description | Secret |
|----------|-------------|--------|
| `GITHUB_PAT` | GitHub Personal Access Token with `repo` scope | Yes |

### 2. Service Connections

#### GitHub Service Connection

Create a GitHub service connection for releasing to the public mirror:

1. Go to **Project Settings** > **Service connections**
2. Click **New service connection** > **GitHub**
3. Name: `github-lcdpossible`
4. Authentication: Personal Access Token (PAT)
5. PAT requires scopes: `repo`, `write:packages`

### 3. Pipeline Creation

#### CI Pipeline

1. Go to **Pipelines** > **New pipeline**
2. Select **Azure Repos Git**
3. Select the repository
4. Choose **Existing Azure Pipelines YAML file**
5. Path: `/pipelines/ci.yml`
6. Save and run

#### Release Pipeline

1. Go to **Pipelines** > **New pipeline**
2. Select **Azure Repos Git**
3. Select the repository
4. Choose **Existing Azure Pipelines YAML file**
5. Path: `/pipelines/release.yml`
6. Save and run

## Pipeline Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    CI Pipeline                          │
│  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │ Build (Windows) │  │      Build (Linux)          │  │
│  │ - Restore       │  │ - Install VLC, fonts        │  │
│  │ - Build         │  │ - Restore                   │  │
│  │ - Test          │  │ - Build                     │  │
│  │ - Coverage      │  │ - Test                      │  │
│  └─────────────────┘  └─────────────────────────────┘  │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │              Code Format Check                   │   │
│  │              (dotnet format)                     │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                   Release Pipeline                       │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │         Stage: Calculate Version                 │   │
│  │  - Analyze conventional commits                  │   │
│  │  - Generate changelog                            │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │             Stage: Build Artifacts               │   │
│  │  ┌──────────┐ ┌────────────┐ ┌──────────────┐   │   │
│  │  │ win-x64  │ │ linux-x64  │ │ linux-arm64  │   │   │
│  │  └──────────┘ └────────────┘ └──────────────┘   │   │
│  │  ┌──────────┐ ┌────────────┐                    │   │
│  │  │ osx-x64  │ │ osx-arm64  │                    │   │
│  │  └──────────┘ └────────────┘                    │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Stage: Create GitHub Release            │   │
│  │  - Create git tag                                │   │
│  │  - Upload artifacts to GitHub release            │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Version Calculation

The release pipeline uses conventional commits to calculate versions:

| Commit Type | Version Bump |
|-------------|--------------|
| `feat!:` or `BREAKING CHANGE:` | Major (1.0.0 → 2.0.0) |
| `feat:` | Minor (1.0.0 → 1.1.0) |
| `fix:`, `docs:`, `chore:`, etc. | Patch (1.0.0 → 1.0.1) |

## GitHub Mirror

The release pipeline creates releases on the public GitHub mirror at:
https://github.com/DevPossible/lcd-possible

The Azure DevOps repository is the source of truth. The GitHub repository is a public mirror for:
- Open source visibility
- Issue tracking
- Community contributions
- Release downloads

## Troubleshooting

### Pipeline fails to create GitHub release

1. Verify the `github-lcdpossible` service connection exists and has correct permissions
2. Check that the PAT hasn't expired
3. Ensure the PAT has `repo` scope

### Version already exists

If the calculated version already has a tag, the pipeline will automatically bump the patch version until it finds an unused version number.

### Artifact not found

If artifacts aren't found during the release stage:
1. Check that the build stage completed successfully
2. Verify artifact names match between build and release stages
3. Check the artifact path patterns in the release task
