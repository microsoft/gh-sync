using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Patch = Microsoft.VisualStudio.Services.WebApi.Patch;
using Octokit;
using Microsoft.Win32;
using Markdig.Renderers;
using System.Reflection;

namespace gh_sync
{

    public static class Extensions
    {
        internal const string AreaPathName = "area-path";
        internal static string KeyName = @"Software\gh-sync";
        internal static readonly string AreaPath = Extensions.RetreiveOrPrompt(
            AreaPathName,
            prompt: "Please provide an area path for your ADO project organization: ",
            envVarName: "ADO_AREA_PATH"
        );
        internal static string RetreiveOrPrompt(string key, string prompt, string? envVarName = null)
        {
            if (envVarName != null)
            {
                var env = Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrWhiteSpace(env))
                {
                    return env;
                }
            }

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
            try
            {
                var subkey = Registry.CurrentUser.OpenSubKey(KeyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (subkey == null)
                {
                    AnsiConsole.MarkupLine($"[yellow] Registry key {KeyName}\\{key} does not exist.[/]");
                }
                subkey?.DeleteValue(key);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Exception when invalidating old credentials.[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
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
                fields.AddRow(field.Key.ToString(), field.Value?.ToString()?.Replace("[", "[[")?.Replace("]", "]]") ?? "");
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
                if (relation.Title is null) {
                    relation.Title = "";
                }
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

        public static string WorkItemType(this Issue issue)
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
                yield return ("/fields/System.AreaPath", @AreaPath);
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
            $"{issue.Title} ({issue.Repository.Owner.Login}/{issue.Repository.Name}#{issue.Number})";

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
