# Development Notes

## Building Releases

```
dotnet build -c Release
dotnet pack -c Release
dotnet nuget push \Path\To\Package -s https://nuget.org -k $NugetKey
```