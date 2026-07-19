FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PRM.Services.AI/PRM.Services.AI.csproj", "src/PRM.Services.AI/"]
COPY ["src/PRM.Shared/PRM.Shared.csproj", "src/PRM.Shared/"]
RUN dotnet restore "src/PRM.Services.AI/PRM.Services.AI.csproj"
COPY . .
WORKDIR "/src/src/PRM.Services.AI"
RUN dotnet build "PRM.Services.AI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PRM.Services.AI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PRM.Services.AI.dll"]
