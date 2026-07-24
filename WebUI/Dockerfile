FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WebUI/WebUI.csproj", "WebUI/"]
COPY ["BusinessObjects/BusinessObjects.csproj", "BusinessObjects/"]
COPY ["DataAccessObjects/DataAccessObjects.csproj", "DataAccessObjects/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Services/Services.csproj", "Services/"]
RUN dotnet restore "WebUI/WebUI.csproj"

COPY . .
RUN dotnet publish "WebUI/WebUI.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebUI.dll"]
