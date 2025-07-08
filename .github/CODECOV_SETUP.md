# Codecov Integration Setup

This guide explains how to set up Codecov integration for the FluentAzure project.

## Prerequisites

- GitHub repository with admin access
- Codecov account (free for public repositories)

## Setup Steps

### 1. Create Codecov Account

1. Go to [https://app.codecov.io/](https://app.codecov.io/)
2. Sign up using your GitHub account
3. Grant necessary permissions to Codecov

### 2. Add Repository

1. In Codecov dashboard, click "Add new repository"
2. Select `tonyjoanes/FluentAzure`
3. Copy the repository upload token

### 3. Configure GitHub Secrets

1. Go to your GitHub repository
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add the following secret:
   - Name: `CODECOV_TOKEN`
   - Value: [paste the token from Codecov]

### 4. Codecov Configuration (Optional)

Create a `codecov.yml` file in the root of your repository for custom configuration:

```yaml
coverage:
  status:
    project:
      default:
        target: 80%
        threshold: 1%
    patch:
      default:
        target: 80%
        threshold: 1%

comment:
  layout: "reach,diff,flags,tree,reach"
  behavior: new

ignore:
  - "tests/**/*"
  - "examples/**/*"
  - "**/*.Designer.cs"
  - "**/bin/**/*"
  - "**/obj/**/*"
```

### 5. Verify Setup

1. Push a commit to trigger the GitHub Actions workflow
2. Wait for the build to complete
3. Check the Codecov dashboard for coverage reports
4. Verify that coverage comments appear on pull requests

## Codecov Features

- **Coverage Reports**: Detailed line-by-line coverage information
- **Pull Request Comments**: Automatic coverage analysis on PRs
- **Trend Analysis**: Track coverage changes over time
- **Sunburst Charts**: Visual representation of coverage by directory/file
- **Slack Integration**: Get notifications about coverage changes

## Badges

Add coverage badges to your README:

```markdown
[![codecov](https://codecov.io/gh/tonyjoanes/FluentAzure/branch/main/graph/badge.svg)](https://codecov.io/gh/tonyjoanes/FluentAzure)
```

## Troubleshooting

- **No coverage reports**: Ensure tests are running and generating coverage files
- **Upload fails**: Check that `CODECOV_TOKEN` secret is set correctly
- **Coverage seems low**: Verify that the correct test assemblies are being analyzed

## Additional Resources

- [Codecov Documentation](https://docs.codecov.io/)
- [.NET Core Coverage](https://docs.codecov.io/docs/supported-languages#net-core)
- [GitHub Actions Integration](https://docs.codecov.io/docs/github-actions) 
