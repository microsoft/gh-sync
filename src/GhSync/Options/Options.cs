// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync;

public class Options : IOptions
{
    private const string GHTokenName = "gh-token";
    private const string ADOTokenName = "ado-token";
    public string GetVariable(string varName)
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
            default:
                break;
        }

        return variable;
    }
}