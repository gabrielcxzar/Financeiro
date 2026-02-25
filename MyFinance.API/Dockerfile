FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyFinance.API.csproj", "./"]
RUN dotnet restore "MyFinance.API.csproj"
COPY . .
RUN dotnet publish "MyFinance.API.csproj" -c Release -o /app/publish

# Usa a imagem de runtime para rodar a aplicação
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Configuração para o Render (Porta 10000)
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "MyFinance.API.dll"]