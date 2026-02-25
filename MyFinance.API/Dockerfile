FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyFinance.API.csproj", "./"]
RUN dotnet restore "MyFinance.API.csproj"
COPY . .
RUN dotnet publish "MyFinance.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render injeta PORT em runtime; o Program.cs usa essa variavel.
EXPOSE 10000

ENTRYPOINT ["dotnet", "MyFinance.API.dll"]
