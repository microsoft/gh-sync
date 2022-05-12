// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync.Tests;

using Xunit;
using System;
using Octokit;
using System.Collections.Generic;

public record class ExtensionsTests
{
    [Fact]
    public void WorkItemTypeReturnsCorrectly()
    {
        var bugIssue = newIssue("bug");
        var kindBugIssue = newIssue("Kind-Bug");

        Assert.Equal("Bug", Extensions.WorkItemType(bugIssue));
        Assert.Equal("Bug", Extensions.WorkItemType(kindBugIssue));

        var enhancementIssue = newIssue("enhancement");
        var kindEnhancementIssue = newIssue("Kind-Enhancement");

        Assert.Equal("Task", Extensions.WorkItemType(enhancementIssue));
        Assert.Equal("Task", Extensions.WorkItemType(kindEnhancementIssue));

        var testIssue = newIssue("test");

        Assert.Equal("Bug", Extensions.WorkItemType(testIssue));
    }

    [Fact]
    public void WorkItemStateReturnsCorrectly()
    {
        var openIssue = newIssue(state: ItemState.Open);
        Assert.Equal(("New", "Approved"), openIssue.WorkItemState());

        var doneIssue = newIssue(title: "Resolution-Done", state: ItemState.Closed);
        Assert.Equal(("Closed", "Fixed and verified"), doneIssue.WorkItemState());

        var invalidIssue = newIssue(title: "Resolution-Invalid", state: ItemState.Closed);
        Assert.Equal(("Resolved", "Cannot Reproduce"), invalidIssue.WorkItemState());

        var duplicateIssue = newIssue(title: "Resolution-Duplicate", state: ItemState.Closed);
        Assert.Equal(("Resolved", "Duplicate"), duplicateIssue.WorkItemState());

        var wontFixIssue = newIssue(title: "Resolution-WontFix", state: ItemState.Closed);
        Assert.Equal(("Resolved", "As Designed"), wontFixIssue.WorkItemState());

        var badIssue = newIssue(state: ItemState.Closed);
        Assert.Equal(null, badIssue.WorkItemState());
    }

    private Issue newIssue(string title = "", ItemState state = ItemState.Open) {
        return new Issue(
            "",
            "",
            "",
            "",
            number: 123456,
            state: state,
            title: title,
            body: "",
            closedBy: null,
            user: null,
            labels: new List<Label>{newLabel(title)}.AsReadOnly(),
            assignee: null,
            assignees: new List<User>().AsReadOnly(),
            milestone: null,
            comments: 12,
            pullRequest: null,
            closedAt: null,
            createdAt: DateTimeOffset.Now,
            updatedAt: DateTimeOffset.Now,
            id: 1234567,
            nodeId: "",
            locked: false,
            repository: new Repository(),
            reactions: null
        );
    }

    private Label newLabel(string name = "")
    {
        return new Label(
            id: 1,
            url: "",
            name: name,
            nodeId: "",
            color: "",
            description: "",
            true
        );
    }
}