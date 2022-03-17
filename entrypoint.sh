#!/bin/sh

export ADO_PROJECT=$1
export GH_REPO=$2
export ISSUE_NUM=$3
export ADO_TOKEN=$4
export GITHUB_TOKEN=$5

dotnet /gh-sync.dll pull-gh --dry-run $GH_REPO $ISSUE_NUM
