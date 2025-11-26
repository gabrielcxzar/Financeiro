# Usa a imagem oficial do .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyFinance.API.csproj", "./"]
RUN dotnet restore "MyFinance.API.csproj"
COPY . .
RUN dotnet publish "MyFinance.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# O Railway usa a variável PORT, precisamos expor
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyFinance.API.dll"]