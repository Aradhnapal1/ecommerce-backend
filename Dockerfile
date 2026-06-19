FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Ecommerce_Backend.csproj .

RUN dotnet restore Ecommerce_Backend.csproj /p:ServerBuild=true

COPY . .

RUN dotnet publish Ecommerce_Backend.csproj -c Release -o /app/publish /p:UseAppHost=false /p:ServerBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Ecommerce_Backend.dll"]
