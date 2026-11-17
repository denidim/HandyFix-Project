# ---- Build stage ----
# Read by the builder's own Dockerfile reader. A "syntax" line at the top made
# every build fetch a newer reader from Docker Hub first, which nothing here
# needs, and a Docker Hub outage failed a deploy that way (PROJECT_STATE.md
# Section 3cp). The base images are Microsoft's and come from mcr.microsoft.com.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution/props/project files first so `dotnet restore` is its own cached
# Docker layer -- it only reruns when a .csproj or package version actually
# changes, not on every source-code edit.
COPY src/HandyFix.sln src/Directory.Build.props src/Directory.Packages.props src/Rules.ruleset src/stylecop.json src/
COPY src/HandyFix.Common/HandyFix.Common.csproj src/HandyFix.Common/
COPY src/Data/HandyFix.Data.Common/HandyFix.Data.Common.csproj src/Data/HandyFix.Data.Common/
COPY src/Data/HandyFix.Data.Models/HandyFix.Data.Models.csproj src/Data/HandyFix.Data.Models/
COPY src/Data/HandyFix.Data/HandyFix.Data.csproj src/Data/HandyFix.Data/
COPY src/Services/HandyFix.Services/HandyFix.Services.csproj src/Services/HandyFix.Services/
COPY src/Services/HandyFix.Services.Data/HandyFix.Services.Data.csproj src/Services/HandyFix.Services.Data/
COPY src/Services/HandyFix.Services.Mapping/HandyFix.Services.Mapping.csproj src/Services/HandyFix.Services.Mapping/
COPY src/Services/HandyFix.Services.Messaging/HandyFix.Services.Messaging.csproj src/Services/HandyFix.Services.Messaging/
COPY src/Web/HandyFix.Web.ViewModels/HandyFix.Web.ViewModels.csproj src/Web/HandyFix.Web.ViewModels/
COPY src/Web/HandyFix.Web/HandyFix.Web.csproj src/Web/HandyFix.Web/

# Restored and published for the one platform this image runs on ("-r"). Told
# no platform, the publish carries the native libraries of every platform the
# packages support, Windows and macOS among them: 766 MB against 212 MB.
RUN dotnet restore src/Web/HandyFix.Web/HandyFix.Web.csproj -r linux-x64

# Now copy the actual source and publish. "--self-contained false": .NET itself
# is not packed in, it comes with the base image of the runtime stage below.
COPY src/ src/
RUN dotnet publish src/Web/HandyFix.Web/HandyFix.Web.csproj -c Release -r linux-x64 --self-contained false -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The base image comes with a user that has no rights of its own, "app", but
# does not switch to it: a container runs as root unless told otherwise. /keys
# is where the compose files mount the volume with the data protection keys. A
# new, empty volume takes its owner from the folder it is mounted over, so the
# folder is made here and handed to that user before the switch.
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID

# ASPNETCORE_HTTP_PORTS defaults to 8080 on this base image; kept explicit
# here so the exposed port and the app's actual listening port can never
# drift apart silently.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HandyFix.Web.dll"]
