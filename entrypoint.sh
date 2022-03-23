#!/bin/sh

export ADO_URL=$1
export ADO_PROJECT=$2
export ADO_AREA_PATH=$3
export GH_REPO=$4
export ISSUE_NUM=$5
export ADO_TOKEN=$6
export GITHUB_TOKEN=$7

dotnet /gh-sync.dll pull-gh $GH_REPO $ISSUE_NUM
