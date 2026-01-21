FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["BestelApp_API/BestelApp_API.csproj", "BestelApp_API/"]
COPY ["BestelApp_Shared/BestelApp_Shared.csproj", "BestelApp_Shared/"]
COPY ["BestelApp_Models/BestelApp_Models.csproj", "BestelApp_Models/"]
RUN dotnet restore "BestelApp_API/BestelApp_API.csproj"
COPY . .
WORKDIR "/src/BestelApp_API"
RUN dotnet build "BestelApp_API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BestelApp_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BestelApp_API.dll"]
