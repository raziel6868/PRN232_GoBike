FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["API/API.csproj", "API/"]
COPY ["BusinessObjects/BusinessObjects.csproj", "BusinessObjects/"]
COPY ["DataAccessObjects/DataAccessObjects.csproj", "DataAccessObjects/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Services/Services.csproj", "Services/"]
RUN dotnet restore "API/API.csproj"

COPY . .
RUN dotnet publish "API/API.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
