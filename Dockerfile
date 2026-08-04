# syntax=docker/dockerfile:1

# ---- Build stage ----
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
COPY src/Web/HandyFix.Web.Infrastructure/HandyFix.Web.Infrastructure.csproj src/Web/HandyFix.Web.Infrastructure/
COPY src/Web/HandyFix.Web.ViewModels/HandyFix.Web.ViewModels.csproj src/Web/HandyFix.Web.ViewModels/
COPY src/Web/HandyFix.Web/HandyFix.Web.csproj src/Web/HandyFix.Web/

RUN dotnet restore src/Web/HandyFix.Web/HandyFix.Web.csproj

# Now copy the actual source and publish.
COPY src/ src/
RUN dotnet publish src/Web/HandyFix.Web/HandyFix.Web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# ASPNETCORE_HTTP_PORTS defaults to 8080 on this base image; kept explicit
# here so the exposed port and the app's actual listening port can never
# drift apart silently.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HandyFix.Web.dll"]
