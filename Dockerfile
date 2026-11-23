# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["APICombination/APICombination.csproj", "APICombination/"]
RUN dotnet restore "APICombination/APICombination.csproj"

COPY . .
WORKDIR "/src/APICombination"
RUN dotnet publish "APICombination.csproj" \
	--configuration Release \
	--output /app/publish \
	/p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DisableHttpsRedirection=true
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "APICombination.dll"]
