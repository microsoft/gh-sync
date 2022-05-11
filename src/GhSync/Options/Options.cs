// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync;

using Octokit;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

public class Options : IOptions
{
    private const string GHTokenName = "gh-token";
    private const string ADOTokenName = "ado-token";
    internal const string ADOUriName = "ado-uri";
    internal const string AdoProjectName = "ado-project";

    internal static readonly string _CollectionUri = Extensions.RetreiveOrPrompt(
        ADOUriName,
        prompt: "Please provide a URI for your ADO project organization: ",
        envVarName: "ADO_URL"
    );
    internal static readonly string _ProjectName = Extensions.RetreiveOrPrompt(
        AdoProjectName,
        prompt: "Please provide a name for your ADO project: ",
        envVarName: "ADO_PROJECT"
    );

    public string GetToken(string varName)
    {
        string variable = "";

        switch (varName)
        {
            case GHTokenName:
                variable = Extensions.RetreiveOrPrompt(
                    GHTokenName,
                    prompt: "Please provide a PAT for use with GitHub: ",
                    envVarName: "GITHUB_TOKEN"
                );
                break;
            case ADOTokenName:
                variable = Extensions.RetreiveOrPrompt(
                    ADOTokenName,
                    prompt: "Please provide a PAT for use with Azure DevOps: ",
                    envVarName: "ADO_TOKEN"
                );
                break;
            default:
                break;
        }

        return variable;
    }

    public async Task<Organization?> GetOrgProfile(IGitHubClient ghClient, string OrgName)
    {
        return await ghClient.Organization.Get(OrgName);
    }

    public VssConnection GetVssConnection(string adoToken)
    {
        var creds = new VssBasicCredential(string.Empty, adoToken);
        var connection = new VssConnection(new Uri(Options._CollectionUri), creds);

        return connection;
    }
}