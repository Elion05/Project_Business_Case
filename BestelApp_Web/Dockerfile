FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["BestelApp_Web/BestelApp_Web.csproj", "BestelApp_Web/"]
COPY ["BestelApp_Shared/BestelApp_Shared.csproj", "BestelApp_Shared/"]
COPY ["BestelApp_Models/BestelApp_Models.csproj", "BestelApp_Models/"]
RUN dotnet restore "BestelApp_Web/BestelApp_Web.csproj"
COPY . .
WORKDIR "/src/BestelApp_Web"
RUN dotnet build "BestelApp_Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BestelApp_Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BestelApp_Web.dll"]
