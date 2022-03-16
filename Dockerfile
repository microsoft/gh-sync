# Set the base image as the .NET 6.0 SDK (this includes the runtime)
FROM mcr.microsoft.com/dotnet/sdk:6.0 as build-env

# Copy everything and publish the release (publish implicitly restores and builds)
COPY . ./
RUN dotnet publish ./gh-sync.csproj -c Release -o out --no-self-contained

# Label the container
LABEL maintainer="Isreal Miles <>"
LABEL repository="https://github.com/microsoft/gh-sync"
LABEL homepage="https://github.com/microsoft/gh-sync"

# Label as GitHub action
LABEL com.github.actions.name="The name of your GitHub Action"
# Limit to 160 characters
LABEL com.github.actions.description="The description of your GitHub Action."
# See branding:
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="activity"
LABEL com.github.actions.color="orange"

# Relayer the .NET SDK, anew with the build output
FROM mcr.microsoft.com/dotnet/sdk:6.0
COPY --from=build-env /out .
COPY entrypoint.sh /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
