// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace gh_sync;

public interface IOptions
{
    string GetToken(string varName);
}