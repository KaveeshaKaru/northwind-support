# The API as a container image. Build it from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# or let docker compose do it. Nothing secret is built in: the connection string, the JWT
# signing key and the environment name all come from environment variables at run time.

# ---- build: the .NET SDK restores, compiles and publishes the API ------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Docker sets TARGETARCH to the CPU the image is built for (arm64 on Apple Silicon, amd64 on
# Intel). Publishing for that one Linux target leaves out the native libraries for Windows and
# macOS that a portable build carries.
ARG TARGETARCH

# Restore from only the files that decide which packages are needed. Docker caches this
# layer, so changing a .cs file skips straight past the package download.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj -a $TARGETARCH

COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -a $TARGETARCH --configuration Release --no-restore --output /app/publish \
    -p:UseAppHost=false -p:SatelliteResourceLanguages=en

# ---- runtime: only the ASP.NET runtime and the published app -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Owned by root and only readable by the app user, so the running app cannot rewrite itself.
COPY --from=build /app/publish .

# Plain HTTP inside the container; TLS is terminated in front of it in a real deployment.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The non-root user the official .NET images provide.
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
