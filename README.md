# gh-sync: Tool for pulling public issues into private tracking

## Prerequisites

- Windows 10
- .NET 6 Preview 7 or later
- A personal access token (PAT) for Azure DevOps
- A personal access token (PAT) for GitHub

## Installing

```pwsh
./install.ps1
```

On first use or when tokens expire, you will be prompted to provide each of your PATs.

## Updating

```pwsh
git pull
./install.ps1
```

## Using

```shell
# Create or update an ADO bug for microsoft/iqsharp#500.
gh-sync pull-gh microsoft/iqsharp 500
# Find existing ADO work item or bug for microsoft/iqsharp#500
gh-sync find-ado microsoft/iqsharp 500
# Show a text representation of AB#31795.
gh-sync get-ado 31795
```
