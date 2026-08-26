FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV NUGET_XMLDOC_MODE=skip
ENV DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false

COPY Directory.Build.props ./
COPY NuGet.config ./
COPY src/LibraryManager.Domain/LibraryManager.Domain.csproj src/LibraryManager.Domain/
COPY src/LibraryManager.Application/LibraryManager.Application.csproj src/LibraryManager.Application/
COPY src/LibraryManager.Infrastructure/LibraryManager.Infrastructure.csproj src/LibraryManager.Infrastructure/
COPY src/LibraryManager.Api/LibraryManager.Api.csproj src/LibraryManager.Api/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/LibraryManager.Api/LibraryManager.Api.csproj --disable-parallel

COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/LibraryManager.Api/LibraryManager.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LibraryManager.Api.dll"]
