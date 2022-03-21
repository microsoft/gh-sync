# gh-sync: Tool for pulling public issues into private tracking

If your team manages public GitHub issues for open source repositories as well as internal tracking with Azure Devops (ADO), gh-sync can mirror the two while keeping internal information secure. The gh-sync project includes a Dockerfile from which the project is containerized to be used by a GitHub Action workflow. By using gh-sync as a GitHub Action or command line tools, individual contributors (ICs) are able to increase productivity and visibility as any changes to GitHub issues are reflected with their ADO issue counterparts.

```shell
// Create or update an ADO bug for <GitHub Organization>/<GitHub Project>#500.
gh-sync pull-gh <GitHub Organization>/<GitHub Project> 500

// Create or update all ADO issues with the "tracking" label from <GitHub Organization>/<GitHub Project>.
gh-sync pull-gh <GitHub Organization>/<GitHub Project> 500

// Find existing ADO work item or bug for <GitHub Organization>/<GitHub Project>#500
gh-sync find-ado <GitHub Organization>/<GitHub Project> 500

// Show a text representation of ADO issue #31795.
gh-sync get-ado 31795
```

## Prerequisites

- Windows 10
- .NET 6 Preview 7 or later
- A personal access token (PAT) for Azure DevOps

## Using gh-sync as a GitHub Action

A [GitHub Action](https://github.com/features/actions) is the recommended way to include gh-sync to your repositories. Simply create a new action under `<YourGitRepo>/.github/worfklows/<YourAction>.yml` to specify when gh-sync should be called. Your action can be specified to specific issue events or applied to only specific labels for automatic mirroring between GitHub and ADO issues.

```yml
name: Sync GitHub with ADO

on: 
  issues:
    types: [closed, edited, deleted, reopened, assigned, unassigned, labeled, unlabeled]
  issue_comment:

jobs:
  build:
    name: Run gh-sync from GitHub action
    if: ${{ github.event.label.name == 'tracking' || contains(github.event.issue.labels.*.name, 'tracking') }} # Filters out issues/events without the 'tracking' label
    runs-on: ubuntu-latest
    steps:
      - name: 'Trigger gh-sync'
        uses: microsoft/gh-sync@main
        with:
          ado-organization-url: 'https://<ado-project-url>'
          ado-project: '<ADO Project>'
          ado-area-path: '<ADO Project>/<Team Name>'
          github-repo: '<GitHub Organization>/<GitHub Project>'
          issue-number: ${{github.event.issue.number}} # Auto-generated from GitHub action
          ado-token: ${{ secrets.AZURE_DEVOPS_TOKEN }} # Your Personal Access Token (PAT)
          github-token: ${{ secrets.GITHUB_TOKEN }}    # Auto-generated from GitHub action
```

You can customize gh-sync to fit your own team's specific needs by simply forking this repository and pointing to the new location at the `uses:` keyword.

## Using gh-sync via PowerShell

If you want to use gh-sync manually during your daily work, simply clone this repository and run the `install.ps1` script at the root of gh-sync. This will add gh-sync to your PATH and validate you are running Windows 10 or later.

```pwsh
./install.ps1
```

You will then need to set the following environment variables:

```pwsh
> $Env:ADO_URI='https://<ado-project-uri>'
> $Env:ADO_PROJECT='<ADO Project>'
> $Env:AREA_PATH='<ADO Area Path>'
> $Env:ADO_TOKEN='<Your ADO PAT>'
> $Env:GITHUB_TOKEN='<Your GitHub PAT>'
```

You can now run gh-sync:

```pwsh
> gh-sync pull-gh <GitHub Organization>/<GitHub Project> 10 --dry-run
Getting GitHub issue <GitHub Organization>/<GitHub Project>#10...
Got profile OK!
Got GitHub client.
Got repository: https://github.com/<GitHub Organization>/<GitHub Project>.
Got issue: https://github.com/<GitHub Organization>/<GitHub Project>/pull/10.
Updating existing issue, since --allow-existing was not set.
Not updating new work item in ADO, as --dry-run was set.
```

## Updating

Simply pull the latest version of gh-sync and then run the install script again.

```pwsh
git pull
./install.ps1
```
