FROM oven/bun:1 AS frontend
WORKDIR /app
COPY frontend/package.json frontend/bun.lock ./
RUN bun install --frozen-lockfile
COPY frontend/ ./
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
# The commit count arrives from outside: .dockerignore keeps .git out of the build context, so the
# build in here cannot count for itself. Left at 0 it produces a build that says it is not a
# published one.
ARG BUILD_HEIGHT=0
WORKDIR /src
# Directory.Build.props carries the version every assembly is stamped with — without it the build
# inside the image would produce a different one from the tag outside it.
COPY global.json Bugler.slnx Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/Bugler.Host -c Release -o /out -p:BuildHeight=$BUILD_HEIGHT

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl is here for one reason: a container healthcheck runs inside the container, and the runtime
# image carries nothing that speaks HTTP.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend /out ./
COPY --from=frontend /app/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080 4317 4318
ENTRYPOINT ["dotnet", "Bugler.Host.dll"]
