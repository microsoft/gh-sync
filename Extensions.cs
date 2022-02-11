using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Patch = Microsoft.VisualStudio.Services.WebApi.Patch;
using Octokit;
using Microsoft.Win32;
using Markdig.Renderers;
using Spectre.Console;
using System.Reflection;

namespace gh_sync
{

    internal static class Extensions
    {
        internal static string KeyName = @"Software\gh-sync";
        internal static string RetreiveOrPrompt(string key, string prompt)
        {
            try
            {
                var creds = Registry.GetValue("HKEY_CURRENT_USER\\" + KeyName, key, null);
                if (creds != null && creds is string value)
                {
                    return value;
                }
            }
            catch (Exception) {}

            
            var response = AnsiConsole.Ask<string>(prompt).Trim();
            Registry.SetValue("HKEY_CURRENT_USER\\" + KeyName, key, response);
            return response;
        }

        internal static void Invalidate(string key)
        {
            AnsiConsole.MarkupLine($"[[debug]] Invalidating {KeyName}\\{key}.");
            Registry.CurrentUser.OpenSubKey(KeyName, RegistryKeyPermissionCheck.ReadWriteSubTree)?.DeleteValue(key);
        }

        internal static string ReadableLink(this WorkItem workItem) =>
            // Prefer HTML links if available, but fallback to API
            // URLs.
            workItem.Links.Links.ContainsKey("html")
            ? workItem.Links.Links["html"] switch
            {
                ReferenceLink refLink => refLink.Href,
                _ => workItem.Url
            }
            : workItem.Url;

        internal static string? LinkToString(this object link) =>
            link switch
            {
                ReferenceLink refLink => refLink.Href,
                _ => link.ToString()
            };

        internal static void WriteToConsole(this WorkItem workItem)
        {
            var table = new Table();
            table.AddColumns("", "");
            table.HideHeaders();

            table.AddRow("Work Item", workItem.Url);

            var fields = new Table();
            fields.AddColumns("Key", "Value");
            foreach (var field in workItem.Fields)
            {
                fields.AddRow(field.Key.ToString(), field.Value?.ToString() ?? "");
            }
            table.AddRow(new Markup("Fields"), fields);

            var relations = new Table();
            relations.AddColumns("Title", "Rel", "Url", "Attributes");
            foreach (var relation in workItem.Relations ?? new List<WorkItemRelation>())
            {
                var attrs = relation.Attributes == null
                            ? string.Empty
                            : string.Join("",
                                relation.Attributes.Select(attr =>
                                    $"\n  - {attr.Key}: {attr.Value}"
                                )
                            );
                relations.AddRow(relation.Title, relation.Rel, relation.Url, attrs);
            }
            table.AddRow(new Markup("Relations"), relations);

            var links = new Table();
            links.AddColumns("Key", "Value");
            foreach (var link in workItem.Links.Links)
            {
                links.AddRow(link.Key, link.Value?.LinkToString() ?? "");
            }
            table.AddRow(new Markup("Links"), links);

            AnsiConsole.Render(table);
        }

        internal static bool IsLabeledAs(this Issue issue, params string[] labels) =>
            issue.Labels.Any(issueLabel =>
                labels.Any(label => issueLabel.Name == label)
            );

        internal static string WorkItemType(this Issue issue)
        {
            if (issue.IsLabeledAs("bug", "Kind-Bug"))
            {
                return "Bug";
            }
            else if (issue.IsLabeledAs("enhancement", "Kind-Enhancement"))
            {
                return "Task";
            }
            // Default to "bug" if we don't know anything more specific.
            return "Bug";
        }

        internal static async Task<WorkItem> UpdateState(this WorkItem workItem, Issue issue) =>
            await Ado.WithWorkItemClient(async client =>
            {
                if (issue.WorkItemState() is {} state)
                {
                    return await client.UpdateWorkItemAsync(
                        new Patch.Json.JsonPatchDocument
                        {
                            new Patch.Json.JsonPatchOperation
                            {
                                Operation = Patch.Operation.Replace,
                                Path = "/fields/System.State",
                                Value = state.State
                            }
                        },
                        Ado.ProjectName, workItem.Id!.Value
                    );
                    // TODO: update Reason
                    // return await client.UpdateWorkItemAsync(
                    //     new Patch.Json.JsonPatchDocument
                    //     {
                    //         new Patch.Json.JsonPatchOperation
                    //         {
                    //             Operation = Patch.Operation.Replace,
                    //             Path = "/fields/System.Reason",
                    //             Value = state.Reason
                    //         }
                    //     },
                    //     Ado.ProjectName, workItem.Id!.Value
                    // );
                }
                else
                {
                    AnsiConsole.MarkupLine($"[bold yellow]Status of work item {workItem.ReadableLink()} not updated, as GitHub issue {issue.HtmlUrl} may be missing a triage label.[/]");
                    return workItem;
                }
            });

        internal static string MarkdownToHtml(this string source)
        {
            var writer = new StringWriter();
            var renderer = new HtmlRenderer(writer);
            Markdig.Markdown.Convert(source, renderer);
            writer.Flush();
            return writer.ToString();
        }

        internal static Patch.Json.JsonPatchDocument AsPatch(this Issue issue, Patch.Operation operation = Patch.Operation.Add)
        {
            IEnumerable<(string Path, string Value)> Operations()
            {
                yield return ("/fields/System.Title", $"{issue.WorkItemTitle()}");
                yield return ("/fields/System.AreaPath", @"Quantum Program\Quantum Systems\QDK");
                var htmlBody = issue.Body.MarkdownToHtml();
                var description = $"<h3>Description from <a href=\"{issue.HtmlUrl}\">{issue.Repository.Owner.Login}/{issue.Repository.Name}#{issue.Number}</a> (reported by <a href=\"{issue.User.HtmlUrl}\">@{issue.User.Login}</a>):</h3>\n\n{htmlBody}";
                yield return (
                    issue.WorkItemType() == "Task"
                    ? "/fields/System.Description"
                    : "/fields/Microsoft.VSTS.TCM.ReproSteps",
                    description
                );

                // Issue state cannot be added or updated as a single patch.
                // TODO: Map milestones to iteration path.
            }

            var patch = new Patch.Json.JsonPatchDocument();
            patch.AddRange(
                Operations().Select(op => new Patch.Json.JsonPatchOperation
                {
                    Operation = operation,
                    Path = op.Path,
                    Value = op.Value
                })
            );
            return patch;
        }

        internal static (string State, string Reason)? WorkItemState(this Issue issue)
        {
            if (issue.State.Value == ItemState.Open)
            {
                return ("Active", "Approved");
            }
            if (issue.IsLabeledAs("Resolution-Done"))
            {
                return ("Closed", "Fixed and verified");
            }
            else if (issue.IsLabeledAs("Resolution-Invalid", "resolved: no action"))
            {
                return ("Resolved", "Cannot Reproduce");
            }
            else if (issue.IsLabeledAs("Resolution-Duplicate", "duplicate"))
            {
                return ("Resolved", "Duplicate");
            }
            else if (issue.IsLabeledAs("Resolution-WontFix", "resolved: by design"))
            {
                return ("Resolved", "As Designed");
            }

            return null;
        }

        internal static string WorkItemTitle(this Issue issue) =>
            $"{issue.Repository.Owner.Login}/{issue.Repository.Name}#{issue.Number}: {issue.Title}";

        internal static async Task<TNewResult> Bind<TResult, TNewResult>(this Task<TResult> task, Func<TResult, Task<TNewResult>> continuation) =>
            await continuation(await task);

        internal static async Task<TNewResult> Bind<TResult, TNewResult>(this Task<TResult> task, Func<TResult, TNewResult> continuation) =>
            continuation(await task);

        internal static Issue AddRepoMetadata(this Issue issue, Repository repository)
        {
            // Workaround for https://github.com/octokit/octokit.net/issues/1616.
            issue
                .GetType()
                .GetProperties(
                    BindingFlags.Instance
                    | BindingFlags.GetProperty
                    | BindingFlags.SetProperty
                    | BindingFlags.GetField
                    | BindingFlags.SetField
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                )
                .Single(
                    property => property.Name == "Repository"
                )
                .SetValue(issue, repository);
            return issue!;
        }

    }
}
