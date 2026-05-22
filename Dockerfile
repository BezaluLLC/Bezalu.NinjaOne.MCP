FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.csproj NuGet.Config ./
RUN --mount=type=secret,id=nuget_auth,target=/root/.nuget_auth \
    dotnet nuget update source github \
      --username "$(head -1 /root/.nuget_auth)" \
      --password "$(tail -1 /root/.nuget_auth)" \
      --store-password-in-clear-text \
      --configfile NuGet.Config && \
    dotnet restore -r linux-x64

COPY . .
RUN dotnet publish -c Release -r linux-x64 --no-restore -o /app/publish

# Runtime image — use distroless for minimal attack surface
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["./Bezalu.NinjaOne.MCP"]
