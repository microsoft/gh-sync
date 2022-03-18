#!/bin/sh

export ADO_URI=$1
export ADO_PROJECT=$2
export GH_REPO=$3
export ISSUE_NUM=$4
export ADO_TOKEN=$5
export GITHUB_TOKEN=$6

dotnet /gh-sync.dll pull-gh $GH_REPO $ISSUE_NUM --dry-run
