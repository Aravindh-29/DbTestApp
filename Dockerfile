FROM mcr.microsoft.com/dotnet/sdk:8.0 AS BUILD
WORKDIR /src
COPY ["DbConnectionTester.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish "DbConnectionTester.csproj" -c Release -o /src/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=BUILD /src/publish /app
EXPOSE 8080
ENTRYPOINT ["dotnet", "DbConnectionTester.dll"]