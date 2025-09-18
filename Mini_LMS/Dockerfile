# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy only the Web API project file and restore dependencies
COPY Mini_LMS.csproj ./
RUN dotnet restore Mini_LMS.csproj

# Copy the rest of the Web API source code
COPY . ./

# Publish only the Web API project (ignore tests)
RUN dotnet publish Mini_LMS.csproj -c Release -o out

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./

EXPOSE 5000
ENTRYPOINT ["dotnet", "Mini_LMS.dll"]
