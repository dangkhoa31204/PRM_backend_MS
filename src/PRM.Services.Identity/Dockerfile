FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PRM.Services.Identity/PRM.Services.Identity.csproj", "src/PRM.Services.Identity/"]
RUN dotnet restore "src/PRM.Services.Identity/PRM.Services.Identity.csproj"
COPY . .
WORKDIR "/src/src/PRM.Services.Identity"
RUN dotnet build "PRM.Services.Identity.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PRM.Services.Identity.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PRM.Services.Identity.dll"]
