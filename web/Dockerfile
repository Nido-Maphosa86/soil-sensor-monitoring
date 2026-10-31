# What this file is for: builds the SensorGrid web console into a small container image, so it can be hosted (for example on Render).
# How it fits in: the host builds it from the web folder. Stage 1 compiles with the .NET SDK; stage 2 keeps only the published app on
# the smaller ASP.NET runtime. No secret goes into the image: the Firebase settings and the key file are given by the host
# (environment variables and a secret file), see web/README.md, "Hosting on Render".

# ---- stage 1: build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# the project file first, so the package download is reused when only code changes
COPY src/SensorGrid.Web/SensorGrid.Web.csproj src/SensorGrid.Web/
RUN dotnet restore src/SensorGrid.Web/SensorGrid.Web.csproj
COPY src/SensorGrid.Web/ src/SensorGrid.Web/
RUN dotnet publish src/SensorGrid.Web/SensorGrid.Web.csproj -c Release -o /app --no-restore

# ---- stage 2: run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Report files are saved in App_Data/reports. The app runs as the non-root "app" user, so that user must own the folder.
USER root
RUN mkdir -p /app/App_Data/reports && chown -R $APP_UID /app/App_Data
USER $APP_UID
# the app listens on port 8080 inside the container; the host sends its web traffic there
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SensorGrid.Web.dll"]