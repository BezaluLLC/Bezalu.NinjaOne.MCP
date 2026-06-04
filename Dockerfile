# The chiseled runtime runs as a non-root user (APP_UID). It has no shell, so we
# pre-create the writable data directory in a stage that does, then copy it in with
# the right ownership. This prevents "Permission denied" when persisting OAuth state.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS data-dir
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app

COPY ./publish .

# Copy the empty data directory owned by the non-root runtime user so the app can write.
COPY --from=data-dir --chown=$APP_UID:$APP_UID /data /app/data

ENV ASPNETCORE_URLS=http://+:8080
# Local, cloud-neutral persistence for OAuth clients and tokens. Mount a volume here to
# retain registrations and user sessions across container restarts.
ENV MCP_DATA_PATH=/app/data
VOLUME ["/app/data"]
EXPOSE 8080

ENTRYPOINT ["./Bezalu.NinjaOne.MCP"]
