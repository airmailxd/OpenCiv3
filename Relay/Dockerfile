# The OpenCiv3 relay. Build from this folder: docker build -t openciv3-relay .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Relay.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish Relay.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image's unprivileged user.
USER app
ENTRYPOINT ["dotnet", "Relay.dll"]
