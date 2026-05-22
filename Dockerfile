FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG NUGET_GITHUB_USERNAME
ARG NUGET_GITHUB_TOKEN
ENV NUGET_GITHUB_USERNAME=${NUGET_GITHUB_USERNAME}
ENV NUGET_GITHUB_TOKEN=${NUGET_GITHUB_TOKEN}

COPY *.csproj NuGet.Config ./
RUN dotnet restore -r linux-x64

COPY . .
RUN dotnet publish -c Release -r linux-x64 --no-restore -o /app/publish

# Runtime image — use distroless for minimal attack surface
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["./Bezalu.NinjaOne.MCP"]
