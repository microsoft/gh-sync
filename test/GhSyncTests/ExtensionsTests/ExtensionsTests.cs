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
    public void WorkItemTypeReturnsCorrectTypes()
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

    private Issue newIssue(string title = "") {
        return new Issue(
            "",
            "",
            "",
            "",
            number: 123456,
            ItemState.Open,
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