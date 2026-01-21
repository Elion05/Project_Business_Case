FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["BestelApp_Cons/BestelApp_Cons.csproj", "BestelApp_Cons/"]
COPY ["BestelApp_Shared/BestelApp_Shared.csproj", "BestelApp_Shared/"]
COPY ["BestelApp_Models/BestelApp_Models.csproj", "BestelApp_Models/"]
RUN dotnet restore "BestelApp_Cons/BestelApp_Cons.csproj"
COPY . .
WORKDIR "/src/BestelApp_Cons"
RUN dotnet build "BestelApp_Cons.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BestelApp_Cons.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BestelApp_Cons.dll"]
